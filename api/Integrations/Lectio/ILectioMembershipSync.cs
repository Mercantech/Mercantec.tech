namespace Mercantec.Tech.Api.Integrations.Lectio;

/// <summary>
/// Kontrakt til senere sync af Lectio-hold → Membership (Sprint 1 stub).
/// </summary>
public interface ILectioMembershipSync
{
    /// <summary>Hent elev-id for Auth-sub fra Lectio-Collector (hvis koblet).</summary>
    Task<string?> ResolveLectioIdAsync(Guid authSub, string bearerToken, CancellationToken ct = default);

    /// <summary>
    /// Planlagt: synkronisér hold-roster til Membership med Source=lectio.
    /// Må ikke slette/overskrive Source=manual.
    /// </summary>
    Task<LectioSyncResult> SyncHoldMembershipsAsync(
        Guid holdId,
        string lectioHoldKey,
        string bearerToken,
        CancellationToken ct = default);
}

public sealed record LectioSyncResult(
    bool Implemented,
    int Upserted,
    int SkippedManual,
    string Message);
