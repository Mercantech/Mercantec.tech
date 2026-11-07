using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Notebooks;

public static class NotebookDocumentValidation
{
    public const int MaxStateBytes = 16 * 1024 * 1024;
    public static bool ValidProjection(StoreNotebookDocument? projection, out byte[] state)
    {
        state = [];
        if (projection is null || projection.StateBase64 is null || projection.StateBase64.Length > 24 * 1024 * 1024
            || projection.Fingerprint is not { Length: 64 }
            || !projection.Fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')
            || projection.PlainText is null || projection.PlainText.Length > MaxStateBytes
            || projection.LinkIds is null || projection.AssetIds is null
            || projection.LinkIds.Length > 100_000 || projection.AssetIds.Length > 100_000
            || projection.LinkIds.Contains(Guid.Empty) || projection.AssetIds.Contains(Guid.Empty)
            || projection.DrawingIds is { Length: > 1000 } || projection.DrawingIds?.Contains(Guid.Empty) == true
            || projection.DrawingIds?.Distinct().Count() != projection.DrawingIds?.Length) return false;
        try { state = Convert.FromBase64String(projection.StateBase64); }
        catch (FormatException) { return false; }
        return state.Length is > 0 and <= MaxStateBytes;
    }
}
