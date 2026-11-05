# LUP document collaboration

Self-hosted Hocuspocus 4 + Yjs 13 service for `/notion`. This service has no paid
cloud dependency. The app's ASP.NET API is the authority for JWT verification,
hold permissions and durable Postgres storage.

## Runtime and configuration

Node.js 22 or later is required. The Dockerfile uses Node 22 and runs as the
unprivileged `node` user. Build with `collaboration/` as the Docker build context.

| Variable | Value |
|---|---|
| `API_BASE_URL` | Internal API origin, normally `http://mercantec-tech-api:8080` |
| `NOTION_SERVICE_SECRET` | At least 32 characters; same value as API `Notion__ServiceSecret` |
| `NOTION_SERVICE_SECRET_FILE` | Shared key file, normally `/run/notion/service-key`; used when the explicit secret is blank |
| `PORT` | `1234` |
| `HOST` | `0.0.0.0` |
| `ALLOWED_ORIGINS` | Comma-separated browser origins; production `https://mercantec.tech` |

The Compose initializer creates a random key in the persistent shared key volume
on first startup, so deployment requires no manually provisioned secret. It uses
exclusive creation, preserves and validates an existing key, and never prints it.
Both the API and collaboration service mount the key read-only. An explicit
`NOTION_SERVICE_SECRET` environment value can override it when necessary. Startup
fails closed when the key is missing or too short. The service key only reaches the API's
internal persistence routes; user JWTs only reach the authorization route.

```sh
npm ci
npm test
npm start
```

Proxy the same-origin `/collaboration` WebSocket endpoint to this service on port
1234 with HTTP/1.1 Upgrade support. HTTP `GET /health` is the container liveness
check. No document bytes are exposed through HTTP here.

## Protocol and storage

- Document names are `notion:<page-guid>`.
- Providers send the current LUP access JWT as the Hocuspocus token.
- Each connection is authorized by `POST /api/notion/collaboration/authorize`.
  Current pilot permissions require managing the specific page's hold.
- Authorization is rechecked every 15 seconds, including idle sessions. Every
  inbound message checks JWT expiry, and an expiry timer closes idle sessions.
- Yjs binary state is loaded and saved through the internal API, using
  `X-Notion-Service-Key`. Only an explicit 204 response means a new empty document.
  Failed loads never silently create a replacement document.
- Save debouncing is 750 ms, capped at 3 seconds during continuous editing.
  Failed writes remain dirty in Hocuspocus and are retried. A process crash can
  lose changes which have not yet received a durable acknowledgement; the UI
  must continue showing those changes as unsaved.
- After a successful database write, the server sends a stateless JSON event:

```json
{
  "type": "persisted",
  "savedAtUtc": "2026-11-05T12:00:00Z",
  "stateVector": "base64-encoded-vector",
  "stateHash": "sha256-hex-of-persisted-binary",
  "fingerprintHash": "sha256-hex-of-normalized-state-vector-and-delete-set"
}
```

The editor compares `fingerprintHash` and guards against local changes while
hashing before displaying “Gemt i skyen”. A delete need not advance a state
vector, and UndoManager can retain deleted structs that the server garbage
collects, making binary hashes differ for the same collaborative state.
The canonical fingerprint is SHA-256 of UTF-8 `JSON.stringify({stateVector,
deleteSet})`: numerically sorted `[client,clock]` pairs, followed by numerically
sorted `[client, [[clock,len],...]]` deletion ranges. Sort each client's ranges
and merge overlapping or contiguous ranges. `stateHash` is diagnostic only.
The server does not issue an old acknowledgement if the live document changed
during its persistence request. Incoming client stateless broadcasts are rejected
to prevent forged save notifications.

A failed write emits `{"type":"persistenceError","message":"..."}`. A connected
or synchronized provider alone is not proof of durable storage.

Every binary storage PUT includes `X-Notion-State-Fingerprint`, the 64-character
lowercase hexadecimal canonical fingerprint of that exact snapshot. The API
stores it alongside the binary and advances the draft revision only if the
fingerprint changes. Loading an older document stamps this metadata before
synchronization; identical re-saves do not create false unpublished changes.
During a rolling replacement, a v1 server with the valid internal service key
may flush without the header. The API preserves those bytes and clears the
fingerprint, making them unpublishable until the v2 server stamps the state.
An explicitly supplied invalid fingerprint remains an error.

Publishing is an ASP.NET operation that conditionally copies the persisted binary,
title and attachment grants into a separate immutable snapshot. Its expected
revision and fingerprint must still match. Students read that snapshot through
the authorized page context, without joining this service or receiving draft
state. Only teachers with management access can authenticate to draft rooms.

WebSocket clients must connect with `?schemaVersion=4`. Missing or older schema
versions are rejected with `editor_version_mismatch` before authorization or
document loading. This forces already-open v1 browser tabs to reload before
they can edit documents containing the newer reference and media node types.

## Operational limits

The pilot runs **one collaboration replica**. Multiple independent replicas would
race to overwrite full binary states without a shared coordinator; add a suitable
Redis/shared-document design before scaling the service horizontally. Keep all
page metadata, Yjs state and file storage in the platform's normal backup plan.

WebSocket messages are capped at 2 MiB; complete stored documents at 16 MiB.
Pre-authentication buffering and per-session message rates are bounded. Attachments
belong in object storage and are referenced by ID, rather than inserted as data
URLs into the collaborative state. The editor and service pin compatible provider
versions in lockfiles.

## Tests

`npm test` covers scoped authentication, missing service credentials, storage
failures, two real providers editing concurrently, binary reload after restart,
revoked access, expired tokens, forged save messages and the origin allowlist.
The tests run against an isolated in-memory API stub and never contact real
hold, student or document records.

Hocuspocus and Yjs are MIT-licensed. Their license notices are included with the
installed packages; preserve them when redistributing the service.
