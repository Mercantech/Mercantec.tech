"""Build the reusable public roulette word bank once, using the local Røst bridge.

Uses Python's standard library only. No model download, package install, cloud API,
student names, or runtime generation. Run --help for the preview and batch commands.
"""
import argparse
from array import array
import hashlib
import io
import json
import math
from pathlib import Path
import sys
import urllib.error
import urllib.parse
import urllib.request
import wave

ROOT = Path(__file__).resolve().parents[1]
SAMPLE_RATE = 24000
MAX_BYTES = 1024 * 1024
MAX_SECONDS = 10
# Must change if the bridge's generation settings change; arena DSP has its own
# processed-bank fingerprint and deliberately does not affect this raw cache.
INFERENCE_PROFILE = "rost-da-temp06-cfg05-exag05-reppen20-topp095-v1"


def words(path):
    catalog = json.loads(path.read_text(encoding="utf-8-sig"))
    clips = []
    for key, prefix in (("starts", "prefix"), ("ends", "suffix")):
        values = catalog[key]
        if len(values) != 24 or len(set(values)) != 24:
            raise ValueError("The word catalog must contain 24 unique prefixes and suffixes")
        for index, text in enumerate(values):
            if not isinstance(text, str) or not text.strip() or len(text) > 40 or any(ord(c) < 32 for c in text):
                raise ValueError("Invalid word in roulette catalog")
            clip_id = f"{prefix}-{index:02d}"
            clips.append({"id": clip_id, "text": text, "file": f"{clip_id}.wav"})
    return clips


def decode_wav(data):
    if not 44 < len(data) <= MAX_BYTES:
        raise ValueError("Empty or oversized word recording")
    with wave.open(io.BytesIO(data), "rb") as source:
        if (source.getnchannels(), source.getsampwidth(), source.getframerate(), source.getcomptype()) != (1, 2, SAMPLE_RATE, "NONE"):
            raise ValueError("Word recordings must be mono PCM16 WAV at 24 kHz")
        count = source.getnframes()
        if not SAMPLE_RATE // 10 <= count <= SAMPLE_RATE * MAX_SECONDS:
            raise ValueError("Unexpected duration for a single word")
        frames = source.readframes(count)
        if len(frames) != count * 2:
            raise ValueError("Truncated word recording")
    samples = array("h")
    samples.frombytes(frames)
    if sys.byteorder != "little":
        samples.byteswap()
    return samples


def encode_wav(samples):
    pcm = array("h", samples)
    if sys.byteorder != "little":
        pcm.byteswap()
    output = io.BytesIO()
    with wave.open(output, "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(SAMPLE_RATE)
        target.writeframes(pcm.tobytes())
    return output.getvalue()


def prepare_word(data):
    """Trim silence and add a restrained arena PA effect without claiming new prosody."""
    samples = decode_wav(data)
    peak = max(abs(sample) for sample in samples)
    if peak < 64:
        raise ValueError("Word recording is silent")
    window = SAMPLE_RATE // 100  # 10 ms RMS avoids trimming at individual zero crossings.
    threshold = max(32, peak * 0.012)
    active = [index for index in range(0, len(samples), window)
              if math.sqrt(sum(value * value for value in samples[index:index + window])
                           / len(samples[index:index + window])) > threshold]
    if not active:
        raise ValueError("Word recording contains no speech")
    # Keep a little breath/consonant room, but no model-generated multi-second gaps.
    start = max(0, active[0] - int(SAMPLE_RATE * 0.035))
    end = min(len(samples), active[-1] + window + int(SAMPLE_RATE * 0.070))
    samples = samples[start:end]
    if len(samples) < SAMPLE_RATE // 10:
        raise ValueError("Word recording is too short after silence removal")
    # A little bass removal/presence and gentle soft compression give the voice
    # a PA-speaker character. They cannot make a neutral performance shout.
    previous_input = previous_output = 0.0
    filtered = []
    highpass = math.exp(-2 * math.pi * 90 / SAMPLE_RATE)
    for sample in samples:
        value = sample / 32768
        body = highpass * (previous_output + value - previous_input)
        previous_input, previous_output = value, body
        filtered.append(math.tanh(1.5 * body) / math.tanh(1.5))
    # Short, quiet early reflections suggest an arena without a long muddy tail.
    # Bake them once so every browser gets the same result with no runtime DSP cost.
    echoes = ((round(SAMPLE_RATE * 0.085), 0.12), (round(SAMPLE_RATE * 0.160), 0.05))
    reflected = filtered + [0.0] * echoes[-1][0]
    for delay, mix in echoes:
        for index, value in enumerate(filtered):
            reflected[index + delay] += value * mix
    gain = (32767 * 10 ** (-2 / 20)) / max(abs(sample) for sample in reflected)
    fade = min(int(SAMPLE_RATE * 0.006), len(reflected) // 2)
    result = array("h")
    for index, sample in enumerate(reflected):
        edge = min(1.0, index / fade, (len(reflected) - index - 1) / fade)
        result.append(round(sample * gain * edge))
    return encode_wav(result)


def atomic_write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(path)


def request_json(base, route, payload=None):
    body = None if payload is None else json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(base + route, data=body,
                                     headers={"Content-Type": "application/json"})
    # No proxies: the offline bridge has to remain loopback-only.
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    with opener.open(request, timeout=180) as response:
        data = response.read(MAX_BYTES + 1)
        if len(data) > MAX_BYTES:
            raise ValueError("Bridge response exceeds the word clip size limit")
        return data


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        raise ValueError("The local speech bridge must not redirect")


def validate_endpoint(value):
    uri = urllib.parse.urlsplit(value)
    if (uri.scheme != "http" or uri.hostname not in ("127.0.0.1", "::1", "localhost")
            or uri.username or uri.password or uri.path not in ("", "/") or uri.query or uri.fragment):
        raise ValueError("Use the local HTTP bridge, for example http://127.0.0.1:8091")
    return value.rstrip("/")


def fingerprint(clips, health):
    source = {"words": clips, "model": health.get("model"), "voiceId": health.get("voiceId"),
              "reference": health.get("arenaReference", False), "processing": "trim-pa90hz-softcompress-room85-160ms-normalize2db-fade6ms-v1"}
    return hashlib.sha256(json.dumps(source, ensure_ascii=False, sort_keys=True).encode()).hexdigest()


def raw_word(base, word, health, raw_cache=None, force=False):
    source = {"model": health.get("model"), "voiceId": health.get("voiceId"),
              "text": word + "!", "style": "arena", "inference": INFERENCE_PROFILE}
    key = hashlib.sha256(json.dumps(source, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    target = raw_cache / (key + ".wav") if raw_cache is not None else None
    if target is not None and not force and target.exists() and target.stat().st_size <= MAX_BYTES:
        try:
            cached = target.read_bytes()
            if max(abs(value) for value in decode_wav(cached)) >= 64:
                return cached
        except (ValueError, wave.Error, EOFError):
            pass
    raw = request_json(base, "/speech", {"text": word + "!", "style": "arena"})
    if max(abs(value) for value in decode_wav(raw)) < 64:
        raise ValueError("Raw word recording is silent")
    if target is not None:
        atomic_write(target, raw)
    return raw


def build_bank(base, output, clips, health, force=False, raw_cache=None):
    stamp = fingerprint(clips, health)
    manifest_path = output / "manifest.json"
    previous = {}
    if manifest_path.exists():
        previous = json.loads(manifest_path.read_text(encoding="utf-8"))
    reusable = {clip["id"]: clip for clip in previous.get("clips", [])} if previous.get("generation") == stamp else {}
    manifest = {"version": 1, "complete": False, "provider": "Røst", "style": "Arenaspeaker",
                "sampleRate": SAMPLE_RATE, "model": health.get("model"),
                "referenceVoice": bool(health.get("arenaReference")),
                "generation": stamp, "clips": []}
    # A half-built bank must never be advertised as ready by the app.
    atomic_write(manifest_path, json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8"))
    for index, clip in enumerate(clips):
        target = output / clip["file"]
        old = reusable.get(clip["id"])
        audio = target.read_bytes() if old and not force and target.exists() and target.stat().st_size <= MAX_BYTES else None
        if audio is not None:
            try:
                decode_wav(audio)
                if hashlib.sha256(audio).hexdigest() != old.get("sha256"):
                    audio = None
            except (ValueError, wave.Error, EOFError):
                audio = None
        if audio is None:
            audio = prepare_word(raw_word(base, clip["text"], health, raw_cache, force))
            atomic_write(target, audio)
        manifest["clips"].append({**clip, "sha256": hashlib.sha256(audio).hexdigest(),
                                  "durationMs": round(len(decode_wav(audio)) / SAMPLE_RATE * 1000)})
        atomic_write(manifest_path, json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8"))
        print(f"{index + 1:02d}/{len(clips)} {clip['text']}", flush=True)
    manifest["complete"] = len(manifest["clips"]) == 48
    atomic_write(manifest_path, json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8"))
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--endpoint", default="http://127.0.0.1:8091")
    parser.add_argument("--words", type=Path, default=ROOT / "shared" / "roulette-voice-words.json")
    parser.add_argument("--output", type=Path, default=ROOT / "web" / "wwwroot" / "audio" / "roulette" / "announcer-v1")
    parser.add_argument("--force", action="store_true", help="Regenerate even unchanged validated words")
    parser.add_argument("--raw-cache", type=Path, default=ROOT / ".tools" / "rost-speech" / "raw",
                        help="Private reusable model output; arena effect changes do not need new inference")
    parser.add_argument("--preview", type=Path, help="Generate only Neon + Opfinderne into this WAV, no public bank")
    args = parser.parse_args()
    base = validate_endpoint(args.endpoint)
    clips = words(args.words)
    health = json.loads(request_json(base, "/health"))
    if not health.get("ready"):
        raise ValueError("Røst is not ready. Wait for /health to return ready=true")
    if not health.get("arenaReference"):
        print("No expressive reference configured: Røst will use its default voice. Intensity control is unsupported.", flush=True)
    if args.preview:
        parts = [prepare_word(raw_word(base, word, health, args.raw_cache, args.force))
                 for word in ("Neon", "Opfinderne")]
        combined = decode_wav(parts[0]) + array("h", [0] * int(SAMPLE_RATE * 0.045)) + decode_wav(parts[1])
        atomic_write(args.preview, encode_wav(combined))
        print(f"Preview saved: {args.preview}", flush=True)
    else:
        result = build_bank(base, args.output, clips, health, args.force, args.raw_cache)
        print(f"Ready: {len(result['clips'])} reusable words, 576 group names. No model needed during roulette.", flush=True)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, urllib.error.URLError, wave.Error, EOFError) as error:
        print(f"Voice bank was not completed: {error}", file=sys.stderr)
        sys.exit(1)
