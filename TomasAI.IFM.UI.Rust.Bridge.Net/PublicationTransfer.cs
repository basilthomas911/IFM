using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Text;
using MessagePack;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;

[MessagePackObject]
public sealed record TransferPath([property: Key("kind")] byte Kind, [property: Key("key")] string Key = "", [property: Key("index")] int Index = 0);
[MessagePackObject]
public sealed record TransferOperation([property: Key("path")] TransferPath[] Path, [property: Key("action")] byte Action,
    [property: Key("value")] byte[]? Value = null, [property: Key("index")] int Index = 0, [property: Key("delete")] int Delete = 0, [property: Key("items")] byte[][]? Items = null);
[MessagePackObject]
public sealed record TransferPatch([property: Key("baseSequence")] ulong BaseSequence, [property: Key("sequence")] ulong Sequence, [property: Key("operations")] TransferOperation[] Operations);

/// <summary>One current publication and one patch per stream. Missed revisions recover with a full snapshot.</summary>
public sealed class PublicationTransfer : IDisposable
{
    readonly object gate = new();
    readonly bool incremental, trace;
    public PublicationTransfer(bool incremental = false, bool trace = false) { this.incremental = incremental; this.trace = trace; }
    byte[] current = []; byte[]? patch; WireTree? tree;
    ulong sequence, previous;
    long producedTicks, sourceTicks;
    public (byte[] Bytes, bool Patch, ulong Sequence, long ProducedTicks, long SourceTicks)? Read(byte[] bytes, ulong cursor)
    {
        lock (gate)
        {
            if (!ReferenceEquals(bytes, current) && current.AsSpan().SequenceEqual(bytes)) current = bytes;
            if (!ReferenceEquals(bytes, current))
            {
                previous = sequence; sequence++; producedTicks = DateTime.UtcNow.Ticks;
                WireTree? next = null;
                try
                {
                    if (incremental) next = new(bytes);
                    sourceTicks = trace ? WireTree.ReadSourceTicks(bytes) : 0;
                    patch = tree is null || next is null ? null : BridgeWire.Serialize(new TransferPatch(previous, sequence, WireTree.Diff(tree, next)));
                    if (patch?.Length >= bytes.Length) patch = null;
                }
                catch (InvalidOperationException) { patch = null; next?.Dispose(); next = null; sourceTicks = 0; }
                catch (MessagePackSerializationException) { patch = null; next?.Dispose(); next = null; sourceTicks = 0; }
                catch (EndOfStreamException) { patch = null; next?.Dispose(); next = null; sourceTicks = 0; }
                tree?.Dispose(); tree = next; current = bytes;
            }
            if (cursor == sequence) return null;
            return cursor == previous && patch is not null ? (patch, true, sequence, producedTicks, sourceTicks) : (current, false, sequence, producedTicks, sourceTicks);
        }
    }
    public void Dispose() { lock (gate) { tree?.Dispose(); tree = null; current = []; patch = null; } }
}

/// <summary>Pooled flat MessagePack tree. Leaf values remain slices of the owned publication, including embedded row documents.</summary>
sealed class WireTree : IDisposable
{
    struct Node { public int Offset, Length, First, Next, Count; public byte Kind; public string? Key; }
    static readonly ArrayPool<Node> Pool = ArrayPool<Node>.Create(131072, 2);
    static readonly ConcurrentDictionary<string, string> Keys = new(StringComparer.Ordinal);
    readonly byte[] bytes; Node[] nodes = Pool.Rent(256); int count;
    public long SourceTicks { get; private set; }
    public WireTree(byte[] bytes)
    {
        this.bytes = bytes;
        try { var reader = new MessagePackReader(bytes); Parse(ref reader, 0, 0, null); if (!reader.End) throw new InvalidOperationException("Trailing publication bytes"); }
        catch { Dispose(); throw; }
    }
    int Add()
    {
        if (count >= 131072) throw new InvalidOperationException("Publication exceeds incremental tree budget");
        if (count == nodes.Length) { var larger = Pool.Rent(nodes.Length * 2); nodes.AsSpan(0, count).CopyTo(larger); Array.Clear(nodes, 0, count); Pool.Return(nodes); nodes = larger; }
        var index = count++; nodes[index] = new() { First = -1, Next = -1 }; return index;
    }
    int Parse(ref MessagePackReader reader, int origin, int depth, string? key)
    {
        if (depth > 64) throw new InvalidOperationException("Publication nesting exceeds incremental budget");
        var index = Add(); nodes[index].Offset = origin + checked((int)reader.Consumed); nodes[index].Key = key;
        var kind = reader.NextMessagePackType;
        if (kind is MessagePackType.Map or MessagePackType.Array)
        {
            nodes[index].Kind = kind == MessagePackType.Map ? (byte)1 : (byte)2;
            var length = kind == MessagePackType.Map ? reader.ReadMapHeader() : reader.ReadArrayHeader(); nodes[index].Count = length; int previous = -1;
            for (var i = 0; i < length; i++)
            {
                var childKey = kind == MessagePackType.Map ? ReadKey(ref reader) : null;
                var child = Parse(ref reader, origin, depth + 1, childKey);
                if (previous < 0) nodes[index].First = child; else nodes[previous].Next = child; previous = child;
            }
        }
        else if (kind == MessagePackType.Binary)
        {
            var binary = reader.ReadBytes();
            if (binary is { Length: > 0 } data && data.Length < 16 * 1024 * 1024)
            {
                var offset = origin + checked((int)(reader.Consumed - data.Length));
                var nested = new MessagePackReader(bytes.AsMemory(offset, (int)data.Length));
                if (nested.NextMessagePackType is MessagePackType.Map or MessagePackType.Array)
                {
                    nodes[index].Kind = 3;
                    // Parsing can grow and replace the pooled array; assign only after it returns.
                    var child = Parse(ref nested, offset, depth + 1, null);
                    nodes[index].First = child;
                    if (!nested.End) throw new InvalidOperationException("Not a nested display document");
                }
            }
        }
        else
        {
            if (key == "lastQuoteReceivedAtUtc" && kind == MessagePackType.String)
            {
                var data = reader.ReadStringSequence();
                if (data is { } text && text.IsSingleSegment && Utf8Parser.TryParse(text.FirstSpan, out DateTimeOffset observed, out _, 'O')) SourceTicks = Math.Max(SourceTicks, observed.UtcTicks);
            }
            else reader.Skip();
        }
        nodes[index].Length = origin + checked((int)reader.Consumed) - nodes[index].Offset; return index;
    }
    public static long ReadSourceTicks(byte[] bytes)
    {
        var reader = new MessagePackReader(bytes); long latest = 0; int budget = 131072; Scan(ref reader, 0, ref latest, ref budget); return latest;
        static void Scan(ref MessagePackReader reader, int depth, ref long latest, ref int budget)
        {
            if (depth > 64 || --budget < 0) throw new InvalidOperationException("Timestamp scan budget exceeded");
            var kind = reader.NextMessagePackType;
            if (kind == MessagePackType.Map)
            {
                var count = reader.ReadMapHeader(); for (var i = 0; i < count; i++)
                {
                    var key = ReadKey(ref reader);
                    if (key == "lastQuoteReceivedAtUtc" && reader.NextMessagePackType == MessagePackType.String)
                    {
                        var data = reader.ReadStringSequence(); if (data is { } text && text.IsSingleSegment && Utf8Parser.TryParse(text.FirstSpan, out DateTimeOffset observed, out _, 'O')) latest = Math.Max(latest, observed.UtcTicks);
                    }
                    else Scan(ref reader, depth + 1, ref latest, ref budget);
                }
            }
            else if (kind == MessagePackType.Array) { var count = reader.ReadArrayHeader(); for (var i = 0; i < count; i++) Scan(ref reader, depth + 1, ref latest, ref budget); }
            else if (kind == MessagePackType.Binary) { var data = reader.ReadBytes(); if (data is { Length: > 0 } nestedBytes) { var nested = new MessagePackReader(nestedBytes); if (nested.NextMessagePackType is MessagePackType.Map or MessagePackType.Array) Scan(ref nested, depth + 1, ref latest, ref budget); } }
            else reader.Skip();
        }
    }
    static string ReadKey(ref MessagePackReader reader)
    {
        var data = reader.ReadStringSequence() ?? throw new InvalidOperationException("Incremental map keys must be strings");
        if (!data.IsSingleSegment || data.Length > 256) return Encoding.UTF8.GetString(data.ToArray());
        Span<char> chars = stackalloc char[256]; var length = Encoding.UTF8.GetChars(data.FirstSpan, chars);
        var lookup = Keys.GetAlternateLookup<ReadOnlySpan<char>>(); if (lookup.TryGetValue(chars[..length], out var found)) return found;
        var value = new string(chars[..length]); if (Keys.Count < 1024) Keys.TryAdd(value, value); return value;
    }
    bool Equal(int index, WireTree other, int right) => nodes[index].Length == other.nodes[right].Length && Raw(index).SequenceEqual(other.Raw(right));
    ReadOnlySpan<byte> Raw(int index) => bytes.AsSpan(nodes[index].Offset, nodes[index].Length);
    int Find(int index, string? key) { for (var child = nodes[index].First; child >= 0; child = nodes[child].Next) if (nodes[child].Key == key) return child; return -1; }
    static int[] Children(WireTree tree, int index) { var result = ArrayPool<int>.Shared.Rent(Math.Max(1, tree.nodes[index].Count)); int i = 0; for (var child = tree.nodes[index].First; child >= 0; child = tree.nodes[child].Next) result[i++] = child; return result; }
    public static TransferOperation[] Diff(WireTree before, WireTree after) { var changes = new List<TransferOperation>(); Visit(before, 0, after, 0, new(), changes); return changes.ToArray(); }
    static void Visit(WireTree a, int left, WireTree b, int right, List<TransferPath> path, List<TransferOperation> changes)
    {
        if (a.Equal(left, b, right)) return;
        var old = a.nodes[left]; var next = b.nodes[right];
        if (old.Kind != next.Kind || next.Kind == 0) { changes.Add(new(path.ToArray(), 0, b.Raw(right).ToArray())); return; }
        if (next.Kind == 3) { path.Add(new(2)); Visit(a, old.First, b, next.First, path, changes); path.RemoveAt(path.Count - 1); return; }
        if (next.Kind == 1)
        {
            for (var child = old.First; child >= 0; child = a.nodes[child].Next) if (b.Find(right, a.nodes[child].Key) < 0) { path.Add(new(0, a.nodes[child].Key!)); changes.Add(new(path.ToArray(), 1)); path.RemoveAt(path.Count - 1); }
            for (var child = next.First; child >= 0; child = b.nodes[child].Next) { path.Add(new(0, b.nodes[child].Key!)); var previous = a.Find(left, b.nodes[child].Key); if (previous < 0) changes.Add(new(path.ToArray(), 0, b.Raw(child).ToArray())); else Visit(a, previous, b, child, path, changes); path.RemoveAt(path.Count - 1); }
            return;
        }
        var oldItems = Children(a, left); var newItems = Children(b, right);
        try
        {
            int prefix = 0, suffix = 0; while (prefix < Math.Min(old.Count, next.Count) && a.Equal(oldItems[prefix], b, newItems[prefix])) prefix++;
            while (suffix < Math.Min(old.Count, next.Count) - prefix && a.Equal(oldItems[old.Count - 1 - suffix], b, newItems[next.Count - 1 - suffix])) suffix++;
            if (old.Count != next.Count) { changes.Add(new(path.ToArray(), 2, Index: prefix, Delete: old.Count - prefix - suffix, Items: Enumerable.Range(prefix, next.Count - prefix - suffix).Select(i => b.Raw(newItems[i]).ToArray()).ToArray())); return; }
            // Bounded newest-first logs: represent one prepend and a tail trim instead of replacing every row.
            if (prefix == 0 && old.Count > 16) for (int shift = 1; shift <= Math.Min(16, next.Count - 1); shift++) if (a.Equal(oldItems[0], b, newItems[shift]))
            {
                var same = true; for (var i = 0; i < next.Count - shift; i++) if (!a.Equal(oldItems[i], b, newItems[i + shift])) { same = false; break; }
                if (same) { changes.Add(new(path.ToArray(), 2, Index: 0, Items: Enumerable.Range(0, shift).Select(i => b.Raw(newItems[i]).ToArray()).ToArray())); changes.Add(new(path.ToArray(), 2, Index: next.Count, Delete: shift, Items: [])); return; }
            }
            for (var i = prefix; i < next.Count - suffix; i++) { path.Add(new(1, Index: i)); Visit(a, oldItems[i], b, newItems[i], path, changes); path.RemoveAt(path.Count - 1); }
        }
        finally { ArrayPool<int>.Shared.Return(oldItems); ArrayPool<int>.Shared.Return(newItems); }
    }
    public void Dispose() { var previous = nodes; nodes = []; Array.Clear(previous, 0, count); count = 0; if (previous.Length > 0) Pool.Return(previous); }
}
