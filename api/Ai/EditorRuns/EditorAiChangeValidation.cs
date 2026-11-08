using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

public sealed record EditorAiModelChange(string Operation, string[] TargetBlockIds, string AfterJson,
    string Explanation, string[] SourceIds, Guid? HunkId = null);

public static class EditorAiChangeValidation
{
    public static Dictionary<string, JsonElement> Blocks(JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Array) throw new ArgumentException("invalid_document");
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        void Visit(JsonElement array, int depth)
        {
            if (depth > 64) throw new ArgumentException("document_too_deep");
            foreach (var block in array.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object || !block.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.String || id.GetString() is not { Length: > 0 and <= 200 } text
                    || !result.TryAdd(text, block.Clone())) throw new ArgumentException("invalid_block_identity");
                if (result.Count > 50000) throw new ArgumentException("document_too_complex");
                if (block.TryGetProperty("children", out var children))
                {
                    if (children.ValueKind != JsonValueKind.Array) throw new ArgumentException("invalid_children");
                    Visit(children, depth + 1);
                }
            }
        }
        Visit(document, 0); return result;
    }

    public static EditorAiRunHunk Validate(EditorAiModelChange change, EditorAiRun run,
        IReadOnlyDictionary<string, JsonElement> blocks, IReadOnlyList<EditorAiSource> sources)
    {
        if (change.Operation is not ("insert_after" or "insert_before" or "replace" or "delete" or "replace_selection"))
            throw new ArgumentException("invalid_operation");
        var ids = change.TargetBlockIds.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0 || ids.Length != change.TargetBlockIds.Length || ids.Any(id => !blocks.ContainsKey(id)))
            throw new ArgumentException("invalid_target_blocks");
        if ((change.Operation is "insert_after" or "insert_before") && ids.Length != 1) throw new ArgumentException("invalid_insert_anchor");
        if (change.Operation != "replace_selection" && ids.Any(id => ids.Any(other => id != other && Descendants(blocks[other]).Contains(id))))
            throw new ArgumentException("nested_target_overlap");
        if ((change.Operation is "replace" or "delete") && ids.Length > 1 && !ConsecutiveSiblings(EditorAiRunStore.Parse(run.DocumentJson), ids))
            throw new ArgumentException("noncontiguous_target_blocks");
        var target = EditorAiRunStore.Deserialize<JsonElement?>(run.TargetJson, null);
        if (run.Scope == "selection")
        {
            if (change.Operation != "replace_selection") throw new ArgumentException("selection_operation_required");
            if (target is not JsonElement selectionTarget || !selectionTarget.TryGetProperty("blockIds", out var selected)
                || selected.ValueKind != JsonValueKind.Array || !selected.EnumerateArray().Select(v => v.GetString()).SequenceEqual(ids))
                throw new ArgumentException("selection_target_changed");
        }
        if (run.Scope != "page" && target is JsonElement initial && initial.TryGetProperty("blockIds", out var allowed)
            && allowed.ValueKind == JsonValueKind.Array)
        {
            var permitted = allowed.EnumerateArray().Select(p => p.GetString()).ToHashSet(StringComparer.Ordinal);
            if (ids.Any(id => !permitted.Contains(id))) throw new ArgumentException("target_outside_scope");
        }
        if (change.Operation == "replace_selection" && (target is null || !target.Value.TryGetProperty("selection", out _)))
            throw new ArgumentException("selection_required");
        var adjusted = EditorAiRunStore.Deserialize<Guid[]>(run.AdjustHunkIdsJson, []);
        EditorAiRunHunk? adjustedHunk = null;
        if (adjusted.Length > 0)
        {
            var candidates = run.Hunks.Where(h => adjusted.Contains(h.Id)
                && EditorAiRunStore.Deserialize<string[]>(h.TargetBlockIdsJson, []).SequenceEqual(ids)
                && (change.HunkId is null || change.HunkId == h.Id)).ToArray();
            if (candidates.Length != 1) throw new ArgumentException("adjustment_target_changed");
            adjustedHunk = candidates[0];
        }
        else if (change.HunkId is not null) throw new ArgumentException("unexpected_hunk_id");
        var after = EditorAiBlockValidator.ValidateBlocks(EditorAiRunStore.Parse(change.AfterJson), blocks, sources);
        if (change.Operation == "delete" && after.Length != 0 || change.Operation != "delete" && after.Length == 0)
            throw new ArgumentException("invalid_after_blocks");
        var knownSources = sources.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (change.SourceIds.Any(id => !knownSources.Contains(id))) throw new ArgumentException("unknown_source");
        var before = ids.Select(id => blocks[id]).ToArray();
        if (change.Operation is "replace" or "delete" or "replace_selection")
        {
            // Unknown/native objects are opaque: a replacement must retain their complete JSON identity.
            var retained = Flatten(after).ToArray();
            var affected = change.Operation == "replace_selection" ? before : Flatten(before);
            foreach (var original in affected.Where(b => !WritableTypes.Contains(b.GetProperty("type").GetString() ?? "")))
                if (!retained.Any(b => EditorAiBlockValidator.Fingerprint(b) == EditorAiBlockValidator.Fingerprint(original)))
                    throw new ArgumentException("native_block_would_be_removed");
        }
        var fingerprint = EditorAiBlockValidator.Fingerprint(JsonSerializer.SerializeToElement(before));
        var afterJson = EditorAiRunStore.Serialize(after);
        var signature = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(change.Operation + "|" + string.Join('|', ids) + "|" + EditorAiBlockValidator.Fingerprint(JsonSerializer.SerializeToElement(after)))));
        var touched = ids.SelectMany(id => Descendants(blocks[id]).Append(id)).ToHashSet();
        var overlapping = run.Hunks.Where(h => (h.Status is "ready" or "accepted") && !adjusted.Contains(h.Id))
            .Where(h => !((change.Operation is "insert_after" or "insert_before") && (h.Operation is "insert_after" or "insert_before")))
            .FirstOrDefault(h => EditorAiRunStore.Deserialize<string[]>(h.TargetBlockIdsJson, []).Any(id => touched.Contains(id)
                    || blocks.TryGetValue(id, out var block) && Descendants(block).Any(touched.Contains))
                && h.Signature != signature);
        if (overlapping is not null) throw new ArgumentException("overlapping_changes");
        return new()
        {
            Id = adjustedHunk?.Id ?? Guid.NewGuid(), RunId = run.Id, Order = run.Hunks.Count, Operation = change.Operation,
            TargetBlockIdsJson = EditorAiRunStore.Serialize(ids), BaseFingerprint = fingerprint,
            BeforeJson = EditorAiRunStore.Serialize(before), AfterJson = afterJson, TargetJson = run.TargetJson,
            SourceIdsJson = EditorAiRunStore.Serialize(change.SourceIds.Distinct()), Explanation = change.Explanation,
            Signature = signature
        };
    }

    private static readonly HashSet<string> WritableTypes = ["paragraph", "heading", "bulletListItem", "numberedListItem", "checkListItem", "toggleListItem", "quote", "codeBlock", "learningGoal", "callout", "mermaidDiagram", "pageLink", "table", "divider", "slideBreak", "entityLink"];
    private static IEnumerable<JsonElement> Flatten(IEnumerable<JsonElement> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                foreach (var child in Flatten(children.EnumerateArray())) yield return child;
        }
    }
    private static IEnumerable<string> Descendants(JsonElement block) => block.TryGetProperty("children", out var children)
        && children.ValueKind == JsonValueKind.Array
            ? Flatten(children.EnumerateArray()).Select(b => b.GetProperty("id").GetString()!).Where(id => id is not null)
            : [];
    private static bool ConsecutiveSiblings(JsonElement array, string[] ids)
    {
        var siblings = array.EnumerateArray().ToArray();
        for (var i = 0; i < siblings.Length; i++)
        {
            if (siblings[i].GetProperty("id").GetString() == ids[0])
                return i + ids.Length <= siblings.Length && siblings.Skip(i).Take(ids.Length).Select(b => b.GetProperty("id").GetString()).SequenceEqual(ids);
            if (siblings[i].TryGetProperty("children", out var children) && ConsecutiveSiblings(children, ids)) return true;
        }
        return false;
    }
}
