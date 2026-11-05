using System.Text;

namespace Mercantec.Tech.Api.Notion;

/// <summary>
/// Reads live internal page references from a BlockNote/Yjs document.
/// Only <c>pageLink</c> blocks and <c>pageMention</c> inline nodes count.
/// Deleted history stays in the update and must not become an edge.
/// </summary>
public static class YjsPageLinks
{
    public static IReadOnlyCollection<Guid> Read(ReadOnlySpan<byte> update)
    {
        var links = new HashSet<Guid>();
        if (update.IsEmpty) return links;
        if (update.Length > 1_500_000) throw new InvalidDataException("yjs update is too large");
        var doc = Doc.Decode(update.ToArray());
        if (doc.Roots.TryGetValue("document-store", out var root)) doc.Collect(root, links);
        return links;
    }

    public static bool TryRead(ReadOnlySpan<byte> update, ISet<Guid> links)
    {
        try
        {
            foreach (var id in Read(update)) links.Add(id);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        {
            return false;
        }
    }

    private readonly record struct Id(ulong Client, ulong Clock);

    private abstract class Struct(Id id, int length)
    {
        public Id Id = id;
        public int Length = length;
        public virtual bool Deleted { get; set; }
    }

    private sealed class Gc(Id id, int length) : Struct(id, length)
    {
        public override bool Deleted { get => true; set { } }
    }

    private sealed class Item(Id id, int length) : Struct(id, length)
    {
        public Id? Origin;
        public Id? RightOrigin;
        public YNode? Parent;
        public Id? ParentId;
        public string? ParentSub;
        public Item? Left;
        public Item? Right;
        public required Content Content;
        public Id Last => Length == 1 ? Id : new Id(Id.Client, Id.Clock + (ulong)Length - 1);
    }

    private abstract class Content
    {
        public abstract int Length { get; }
        public virtual void Integrate(Item item) { }
        public abstract Content Splice(int offset);
    }

    private sealed class StringContent(string text) : Content
    {
        public string Text = text;
        public override int Length => Text.Length;
        public override Content Splice(int offset)
        {
            var right = Text[offset..];
            Text = Text[..offset];
            if (offset > 0 && char.IsHighSurrogate(Text[^1]))
            {
                Text = Text[..^1] + "\uFFFD";
                right = "\uFFFD" + right[1..];
            }
            return new StringContent(right);
        }
    }

    private sealed class AnyContent(object?[] values) : Content
    {
        public object?[] Values = values;
        public override int Length => Values.Length;
        public override Content Splice(int offset)
        {
            var right = Values[offset..];
            Values = Values[..offset];
            return new AnyContent(right);
        }
    }

    private sealed class DeletedContent(int length) : Content
    {
        private int _length = length;
        public override int Length => _length;
        public override void Integrate(Item item) => item.Deleted = true;
        public override Content Splice(int offset)
        {
            var right = _length - offset;
            _length = offset;
            return new DeletedContent(right);
        }
    }

    private sealed class OpaqueContent(int length) : Content
    {
        public override int Length { get; } = length;
        public override Content Splice(int offset) => throw new InvalidDataException("cannot split content");
    }

    private sealed class TypeContent(YNode node) : Content
    {
        public YNode Node = node;
        public override int Length => 1;
        public override void Integrate(Item item) => Node.Item = item;
        public override Content Splice(int offset) => throw new InvalidDataException("cannot split a type");
    }

    private sealed class YNode(string? elementName)
    {
        public string? ElementName = elementName;
        public Item? Item;
        public Item? Start;
        public Dictionary<string, Item> Map = new(StringComparer.Ordinal);
    }

    private sealed class Doc
    {
        public Dictionary<string, YNode> Roots { get; } = new(StringComparer.Ordinal);
        private readonly Dictionary<ulong, List<Struct>> _store = new();

        public static Doc Decode(byte[] bytes)
        {
            var doc = new Doc();
            var cursor = new Cursor(bytes);
            var readers = new List<Reader>();
            var clients = cursor.ReadVarUint();
            if (clients > 10_000) throw new InvalidDataException("too many yjs clients");
            for (ulong c = 0; c < clients; c++)
            {
                var count = cursor.ReadVarUint();
                if (count > 250_000) throw new InvalidDataException("too many yjs structs");
                var client = cursor.ReadVarUint();
                var clock = cursor.ReadVarUint();
                var refs = new List<Struct>(checked((int)count));
                for (ulong i = 0; i < count; i++)
                {
                    var info = cursor.ReadByte();
                    var kind = info & 31;
                    Struct entry;
                    if (kind == 0)
                    {
                        var len = checked((int)cursor.ReadVarUint());
                        entry = new Gc(new Id(client, clock), len);
                    }
                    else if (kind == 10)
                    {
                        throw new InvalidDataException("incomplete yjs update");
                    }
                    else
                    {
                        var hasOrigin = (info & 128) != 0;
                        var hasRight = (info & 64) != 0;
                        var copyParent = !hasOrigin && !hasRight;
                        Id? origin = hasOrigin ? cursor.ReadId() : null;
                        Id? right = hasRight ? cursor.ReadId() : null;
                        YNode? parent = null;
                        Id? parentId = null;
                        if (copyParent)
                        {
                            if (cursor.ReadVarUint() == 1) parent = doc.Root(cursor.ReadString());
                            else parentId = cursor.ReadId();
                        }
                        var parentSub = copyParent && (info & 32) != 0 ? cursor.ReadString() : null;
                        var content = cursor.ReadContent(kind);
                        if (content.Length <= 0) throw new InvalidDataException("empty yjs struct");
                        entry = new Item(new Id(client, clock), content.Length)
                        {
                            Origin = origin, RightOrigin = right, Parent = parent, ParentId = parentId,
                            ParentSub = parentSub, Content = content
                        };
                    }
                    refs.Add(entry);
                    clock += (ulong)entry.Length;
                }
                readers.Add(new Reader(client, refs));
            }
            doc.Integrate(readers);
            doc.ApplyDeletes(cursor);
            if (cursor.Remaining != 0) throw new InvalidDataException("trailing yjs bytes");
            return doc;
        }

        public void Collect(YNode node, ISet<Guid> links)
        {
            var seen = new HashSet<YNode>();
            Walk(node, links, seen);
        }

        private void Walk(YNode node, ISet<Guid> links, HashSet<YNode> seen)
        {
            if (!seen.Add(node)) return;
            for (var child = node.Start; child != null; child = child.Right)
            {
                if (child.Deleted || child.Content is not TypeContent type) continue;
                if (type.Node.ElementName is "pageLink" or "pageMention"
                    && Attribute(type.Node, "pageId") is string pageId
                    && Guid.TryParseExact(pageId, "D", out var id) && id != Guid.Empty)
                    links.Add(id);
                Walk(type.Node, links, seen);
            }
        }

        private static string? Attribute(YNode node, string key)
        {
            if (!node.Map.TryGetValue(key, out var item) || item.Deleted) return null;
            return item.Content is AnyContent any && any.Values.Length > 0 ? any.Values[^1] as string : null;
        }

        private YNode Root(string key)
        {
            if (!Roots.TryGetValue(key, out var node)) Roots[key] = node = new YNode(null);
            return node;
        }

        private ulong State(ulong client)
        {
            if (!_store.TryGetValue(client, out var list) || list.Count == 0) return 0;
            var last = list[^1];
            return last.Id.Clock + (ulong)last.Length;
        }

        private Struct Find(Id id)
        {
            if (!_store.TryGetValue(id.Client, out var list)) throw new InvalidDataException("missing yjs struct");
            var lo = 0;
            var hi = list.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                var entry = list[mid];
                if (id.Clock < entry.Id.Clock) hi = mid - 1;
                else if (id.Clock >= entry.Id.Clock + (ulong)entry.Length) lo = mid + 1;
                else return entry;
            }
            throw new InvalidDataException("missing yjs struct");
        }

        private Item Split(Item left, int diff)
        {
            if (diff <= 0 || diff >= left.Length) throw new InvalidDataException("bad yjs split");
            var right = new Item(new Id(left.Id.Client, left.Id.Clock + (ulong)diff), left.Length - diff)
            {
                Origin = new Id(left.Id.Client, left.Id.Clock + (ulong)diff - 1),
                RightOrigin = left.RightOrigin,
                Parent = left.Parent,
                ParentSub = left.ParentSub,
                Left = left,
                Right = left.Right,
                Content = left.Content.Splice(diff),
                Deleted = left.Deleted
            };
            left.Length = diff;
            left.Right = right;
            if (right.Right != null) right.Right.Left = right;
            if (right.ParentSub != null && right.Right == null && right.Parent != null)
                right.Parent.Map[right.ParentSub] = right;
            var list = _store[left.Id.Client];
            list.Insert(list.IndexOf(left) + 1, right);
            return right;
        }

        private void Integrate(List<Reader> readers)
        {
            readers.Sort((a, b) => a.Client.CompareTo(b.Client));
            var active = readers.Count - 1;
            Reader? Next()
            {
                while (active >= 0 && readers[active].Done) active--;
                return active >= 0 ? readers[active] : null;
            }
            var current = Next();
            if (current == null) return;
            var stack = new Stack<Struct>();
            var head = current.Take();
            var guard = 0;
            while (true)
            {
                if (++guard > 1_000_000) throw new InvalidDataException("yjs update did not converge");
                if (head.Id.Clock != State(head.Id.Client)) throw new InvalidDataException("yjs clock gap");
                if (head is Item item)
                {
                    var missing = Missing(item);
                    if (missing is ulong client)
                    {
                        stack.Push(item);
                        var reader = readers.FirstOrDefault(r => r.Client == client && !r.Done)
                            ?? throw new InvalidDataException("yjs update is missing a dependency");
                        head = reader.Take();
                        continue;
                    }
                    Place(item);
                }
                else Add(head);

                if (stack.Count > 0) head = stack.Pop();
                else if (!current.Done) head = current.Take();
                else
                {
                    current = Next();
                    if (current == null) break;
                    head = current.Take();
                }
            }
        }

        private ulong? Missing(Item item)
        {
            if (item.Origin is Id origin && origin.Client != item.Id.Client && origin.Clock >= State(origin.Client)) return origin.Client;
            if (item.RightOrigin is Id right && right.Client != item.Id.Client && right.Clock >= State(right.Client)) return right.Client;
            if (item.ParentId is Id parent && parent.Client != item.Id.Client && parent.Clock >= State(parent.Client)) return parent.Client;
            return null;
        }

        private void Place(Item item)
        {
            var leftGc = false;
            var rightGc = false;
            if (item.Origin is Id origin)
            {
                var end = CleanEnd(origin);
                if (end is Gc) leftGc = true;
                else
                {
                    item.Left = (Item)end;
                    item.Origin = item.Left.Last;
                }
            }
            if (item.RightOrigin is Id rightOrigin)
            {
                var start = CleanStart(rightOrigin);
                if (start is Gc) rightGc = true;
                else
                {
                    item.Right = (Item)start;
                    item.RightOrigin = item.Right.Id;
                }
            }
            if (leftGc || rightGc) item.Parent = null;
            else if (item.Parent == null && item.ParentId == null)
            {
                if (item.Left != null) { item.Parent = item.Left.Parent; item.ParentSub = item.Left.ParentSub; }
                else if (item.Right != null) { item.Parent = item.Right.Parent; item.ParentSub = item.Right.ParentSub; }
            }
            else if (item.ParentId is Id parentId)
            {
                var parent = Find(parentId);
                item.Parent = parent is Item parentItem && parentItem.Content is TypeContent type ? type.Node : null;
                item.ParentId = null;
            }
            if (item.Parent == null)
            {
                Add(new Gc(item.Id, item.Length));
                return;
            }
            Link(item);
            Add(item);
            item.Content.Integrate(item);
            if (item.Parent.Item is { Deleted: true } || item.ParentSub != null && item.Right != null) item.Deleted = true;
        }

        private Struct CleanEnd(Id id)
        {
            var entry = Find(id);
            if (entry is Item item && id.Clock != item.Id.Clock + (ulong)item.Length - 1)
                Split(item, checked((int)(id.Clock - item.Id.Clock + 1)));
            return entry;
        }

        private Struct CleanStart(Id id)
        {
            var entry = Find(id);
            if (entry is Item item && item.Id.Clock < id.Clock)
                return Split(item, checked((int)(id.Clock - item.Id.Clock)));
            return entry;
        }

        private void Link(Item item)
        {
            if (item.Left == null && (item.Right == null || item.Right.Left != null) || item.Left != null && item.Left.Right != item.Right)
            {
                var left = item.Left;
                Item? o;
                if (left != null) o = left.Right;
                else if (item.ParentSub != null)
                {
                    item.Parent!.Map.TryGetValue(item.ParentSub, out o);
                    while (o?.Left != null) o = o.Left;
                }
                else o = item.Parent!.Start;
                var conflicting = new HashSet<Item>();
                var before = new HashSet<Item>();
                while (o != null && o != item.Right)
                {
                    before.Add(o);
                    conflicting.Add(o);
                    if (Same(item.Origin, o.Origin))
                    {
                        if (o.Id.Client < item.Id.Client) { left = o; conflicting.Clear(); }
                        else if (Same(item.RightOrigin, o.RightOrigin)) break;
                    }
                    else if (o.Origin is Id origin && Find(origin) is Item originItem && before.Contains(originItem))
                    {
                        if (!conflicting.Contains(originItem)) { left = o; conflicting.Clear(); }
                    }
                    else break;
                    o = o.Right;
                }
                item.Left = left;
            }
            if (item.Left != null)
            {
                var right = item.Left.Right;
                item.Right = right;
                item.Left.Right = item;
            }
            else
            {
                Item? right;
                if (item.ParentSub != null)
                {
                    item.Parent!.Map.TryGetValue(item.ParentSub, out right);
                    while (right?.Left != null) right = right.Left;
                }
                else
                {
                    right = item.Parent!.Start;
                    item.Parent.Start = item;
                }
                item.Right = right;
            }
            if (item.Right != null) item.Right.Left = item;
            else if (item.ParentSub != null)
            {
                item.Parent!.Map[item.ParentSub] = item;
                if (item.Left != null) item.Left.Deleted = true;
            }
        }

        private static bool Same(Id? left, Id? right) => left is null && right is null
            || left is Id a && right is Id b && a.Client == b.Client && a.Clock == b.Clock;

        private void Add(Struct entry)
        {
            if (!_store.TryGetValue(entry.Id.Client, out var list)) _store[entry.Id.Client] = list = new();
            if (list.Count > 0)
            {
                var last = list[^1];
                if (last.Id.Clock + (ulong)last.Length != entry.Id.Clock) throw new InvalidDataException("yjs struct gap");
            }
            list.Add(entry);
        }

        private void ApplyDeletes(Cursor cursor)
        {
            var clients = cursor.ReadVarUint();
            for (ulong c = 0; c < clients; c++)
            {
                var client = cursor.ReadVarUint();
                var ranges = cursor.ReadVarUint();
                var state = State(client);
                for (ulong i = 0; i < ranges; i++)
                {
                    var clock = cursor.ReadVarUint();
                    var end = clock + cursor.ReadVarUint();
                    if (clock >= state || !_store.TryGetValue(client, out var list)) continue;
                    if (end > state) end = state;
                    var index = IndexOf(list, clock);
                    if (list[index] is Item first && !first.Deleted && first.Id.Clock < clock)
                    {
                        Split(first, checked((int)(clock - first.Id.Clock)));
                        index++;
                    }
                    while (index < list.Count)
                    {
                        var entry = list[index++];
                        if (entry.Id.Clock >= end) break;
                        if (entry is Item item && !item.Deleted)
                        {
                            var itemEnd = item.Id.Clock + (ulong)item.Length;
                            if (end < itemEnd) Split(item, checked((int)(end - item.Id.Clock)));
                            item.Deleted = true;
                        }
                    }
                }
            }
        }

        private static int IndexOf(List<Struct> list, ulong clock)
        {
            var lo = 0;
            var hi = list.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                var entry = list[mid];
                if (clock < entry.Id.Clock) hi = mid - 1;
                else if (clock >= entry.Id.Clock + (ulong)entry.Length) lo = mid + 1;
                else return mid;
            }
            throw new InvalidDataException("delete outside yjs state");
        }
    }

    private sealed class Reader(ulong client, List<Struct> items)
    {
        public ulong Client = client;
        public List<Struct> Items = items;
        public int Index;
        public bool Done => Index >= Items.Count;
        public Struct Take() => Items[Index++];
    }

    private sealed class Cursor(byte[] data)
    {
        private int _pos;
        public int Remaining => data.Length - _pos;
        public byte ReadByte() => _pos >= data.Length ? throw new InvalidDataException("truncated yjs update") : data[_pos++];
        public ulong ReadVarUint()
        {
            ulong num = 0;
            ulong mult = 1;
            while (_pos < data.Length)
            {
                var r = data[_pos++];
                num += (ulong)(r & 127) * mult;
                if (r < 128) return num;
                mult *= 128;
                if (mult == 0) throw new InvalidDataException("yjs integer overflow");
            }
            throw new InvalidDataException("truncated yjs update");
        }
        public long ReadVarInt()
        {
            var r = ReadByte();
            long num = r & 63;
            var mult = 64L;
            var sign = (r & 64) != 0 ? -1 : 1;
            if ((r & 128) == 0) return sign * num;
            while (_pos <= data.Length)
            {
                r = ReadByte();
                num += (r & 127) * mult;
                if (r < 128) return sign * num;
                mult *= 128;
                if (mult <= 0) throw new InvalidDataException("yjs integer overflow");
            }
            throw new InvalidDataException("truncated yjs update");
        }
        public Id ReadId() => new(ReadVarUint(), ReadVarUint());
        public string ReadString()
        {
            var len = checked((int)ReadVarUint());
            if (len < 0 || _pos + len > data.Length) throw new InvalidDataException("truncated yjs string");
            var text = Encoding.UTF8.GetString(data, _pos, len);
            _pos += len;
            return text;
        }
        public void Skip(int len)
        {
            if (len < 0 || _pos + len > data.Length) throw new InvalidDataException("truncated yjs update");
            _pos += len;
        }
        public object? ReadAny(int depth = 0)
        {
            if (depth > 32) throw new InvalidDataException("yjs value is too deep");
            var marker = 127 - ReadByte();
            return marker switch
            {
                0 => null,
                1 => null,
                2 => ReadVarInt(),
                3 => SkipReturn(4),
                4 => SkipReturn(8),
                5 => SkipReturn(8),
                6 => false,
                7 => true,
                8 => ReadString(),
                9 => ReadObject(depth),
                10 => ReadArray(depth),
                11 => SkipReturn(checked((int)ReadVarUint())),
                _ => throw new InvalidDataException("unknown yjs value")
            };
        }
        private object? SkipReturn(int len) { Skip(len); return null; }
        private Dictionary<string, object?> ReadObject(int depth)
        {
            var count = checked((int)ReadVarUint());
            if (count > 10_000) throw new InvalidDataException("yjs object is too large");
            var obj = new Dictionary<string, object?>(count, StringComparer.Ordinal);
            for (var i = 0; i < count; i++) obj[ReadString()] = ReadAny(depth + 1);
            return obj;
        }
        private object?[] ReadArray(int depth)
        {
            var count = checked((int)ReadVarUint());
            if (count > 10_000) throw new InvalidDataException("yjs array is too large");
            var values = new object?[count];
            for (var i = 0; i < count; i++) values[i] = ReadAny(depth + 1);
            return values;
        }
        public Content ReadContent(int kind) => kind switch
        {
            1 => new DeletedContent(checked((int)ReadVarUint())),
            2 => ReadJsonContent(),
            3 => ReadBinary(),
            4 => new StringContent(ReadString()),
            5 => ReadEmbed(),
            6 => ReadFormat(),
            7 => ReadType(),
            8 => ReadAnyContent(),
            9 => ReadDoc(),
            _ => throw new InvalidDataException("unknown yjs content")
        };
        private Content ReadJsonContent()
        {
            var count = checked((int)ReadVarUint());
            for (var i = 0; i < count; i++) ReadString();
            return new OpaqueContent(count);
        }
        private Content ReadBinary() { Skip(checked((int)ReadVarUint())); return new OpaqueContent(1); }
        private Content ReadEmbed() { ReadString(); return new OpaqueContent(1); }
        private Content ReadFormat() { ReadString(); ReadString(); return new OpaqueContent(1); }
        private Content ReadAnyContent()
        {
            var count = checked((int)ReadVarUint());
            if (count > 10_000) throw new InvalidDataException("yjs content is too large");
            var values = new object?[count];
            for (var i = 0; i < count; i++) values[i] = ReadAny();
            return new AnyContent(values);
        }
        private Content ReadDoc() { ReadString(); ReadAny(); return new OpaqueContent(1); }
        private Content ReadType()
        {
            var type = ReadVarUint();
            var name = type is 3 or 5 ? ReadString() : null;
            if (type > 6) throw new InvalidDataException("unknown yjs type");
            return new TypeContent(new YNode(type == 3 ? name : null));
        }
    }
}
