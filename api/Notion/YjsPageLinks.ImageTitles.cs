namespace Mercantec.Tech.Api.Notion;

public static partial class YjsPageLinks
{
    internal static IReadOnlyDictionary<Guid, string> ReadImageTitles(byte[]? state)
    {
        var titles = new Dictionary<Guid, string>();
        if (state is not { Length: > 0 and <= 16 * 1024 * 1024 }) return titles;
        try
        {
            var doc = Doc.Decode(state);
            if (doc.Roots.TryGetValue("document-store", out var root)) doc.CollectImageTitles(root, titles);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        {
            titles.Clear();
        }
        return titles;
    }

    private sealed partial class Doc
    {
        public void CollectImageTitles(YNode root, Dictionary<Guid, string> titles)
        {
            var seen = new HashSet<YNode> { root };
            var pending = new Stack<Item>();
            if (root.Start is not null) pending.Push(root.Start);
            while (pending.TryPop(out var child))
            {
                if (child.Right is not null) pending.Push(child.Right);
                if (child.Deleted || child.Content is not TypeContent type || !seen.Add(type.Node)) continue;
                var element = type.Node;
                if (element.Start is not null) pending.Push(element.Start);
                if (element.ElementName != "pageMedia" || Attribute(element, "kind") != "image"
                    || !Guid.TryParseExact(Attribute(element, "assetId"), "D", out var id) || id == Guid.Empty
                    || Attribute(element, "displayTitle") is not string title
                    || title.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029')) continue;

                title = title.Trim();
                if (title.Length is > 0 and <= 240) titles.TryAdd(id, title);
            }
        }
    }
}
