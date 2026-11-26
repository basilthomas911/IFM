using System.Buffers.Binary;
using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Analytics.RealtimePersistence;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>Exclusive, checksummed, atomic local ownership of open bars and pending publications.</summary>
public sealed class FuturesTradeSessionBarCheckpointStore : IDisposable
{
    const int MaximumBytes = 4 * 1024 * 1024;
    readonly string root;
    readonly FileStream lease;
    readonly ConcurrentDictionary<string, long> committedEnds = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, FileStream> writers = new(StringComparer.Ordinal);
    const uint Magic = 0x31524246; // FBR1: length-delimited, checksummed snapshots.

    /// <summary>Opens one exclusive recovery directory. Unreadable state fails startup rather than starting empty.</summary>
    public FuturesTradeSessionBarCheckpointStore(string directory)
    {
        root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        lease = new FileStream(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    string PathFor(FuturesTradeSessionBarAccumulatorEntityId id) => Path.Combine(root,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.Format()))) + ".bars");

    /// <summary>Enumerates committed partitions, including old sessions with unacknowledged bars.</summary>
    public IEnumerable<FuturesTradeSessionBarAccumulatorEntityId> Partitions()
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.bars"))
        {
            using var reader = Open(path);
            var id = FuturesTradeSessionBarAccumulatorEntityId.Parse(reader.ReadString());
            if (!string.Equals(path, PathFor(id), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Bar checkpoint identity does not match its filename.");
            yield return id;
        }
    }

    BinaryReader Open(string path)
    {
        if (writers.TryRemove(path, out var writer)) writer.Dispose();
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (file.Length > MaximumBytes + 40L) throw new InvalidDataException("Invalid bar journal length.");
        byte[]? latest = null;
        long end = 0;
        Span<byte> header = stackalloc byte[40];
        while (file.Position < file.Length)
        {
            if (file.Length - file.Position < header.Length) break; // Incomplete, unacknowledged tail.
            file.ReadExactly(header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic) throw new InvalidDataException("Invalid bar journal frame.");
            var size = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
            if (size is < 1 or > MaximumBytes) throw new InvalidDataException("Invalid bar checkpoint length.");
            if (file.Length - file.Position < size) break;
            var bytes = new byte[size]; file.ReadExactly(bytes);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), header[8..]))
                throw new InvalidDataException("Bar checkpoint checksum mismatch. Recovery must not acknowledge input.");
            latest = bytes; end = file.Position;
        }
        if (latest is null) throw new InvalidDataException("No committed bar checkpoint is available.");
        if (file.Length != end) { file.SetLength(end); file.Flush(true); }
        committedEnds[path] = end;
        return new BinaryReader(new MemoryStream(latest, false), Encoding.UTF8, false);
    }

    internal void Restore(FuturesTradeSessionBarAccumulatorEntityId id, FuturesTradeSessionBarAccumulator accumulator)
    {
        var path = PathFor(id);
        if (!File.Exists(path)) return;
        using var reader = Open(path);
        if (reader.ReadString() != id.Format()) throw new InvalidDataException("Bar checkpoint partition mismatch.");
        accumulator.ReadCheckpoint(reader);
        if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing bar checkpoint data.");
    }

    internal void Save(FuturesTradeSessionBarAccumulatorEntityId id, FuturesTradeSessionBarAccumulator accumulator)
    {
        using var data = new MemoryStream(4096);
        using (var writer = new BinaryWriter(data, Encoding.UTF8, true))
        { writer.Write(id.Format()); accumulator.WriteCheckpoint(writer); }
        if (data.Length > MaximumBytes) throw new InvalidDataException("Bar checkpoint exceeds its recovery bound.");
        var bytes = data.GetBuffer().AsSpan(0, checked((int)data.Length));
        Span<byte> header = stackalloc byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], bytes.Length);
        SHA256.HashData(bytes, header[8..]);
        var target = PathFor(id);
        if (committedEnds.TryGetValue(target, out var end) && end + header.Length + bytes.Length <= MaximumBytes)
        {
            // No rename or replay on the ordinary tick path. A full physical flush still precedes success.
            var file = writers.GetOrAdd(target, static path => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read));
            file.Position = end;
            if (file.Length != end) file.SetLength(end);
            file.Write(header); file.Write(bytes); file.Flush(true);
            committedEnds[target] = file.Position;
        }
        else
        {
            if (writers.TryRemove(target, out var prior)) prior.Dispose();
            var temporary = target + ".tmp";
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(header); file.Write(bytes); file.Flush(true); }
            AtomicDurableFile.Replace(temporary, target);
            committedEnds[target] = header.Length + bytes.Length;
        }
    }

    internal void Release(FuturesTradeSessionBarAccumulatorEntityId id)
    {
        if (writers.TryRemove(PathFor(id), out var writer)) writer.Dispose();
    }

    /// <summary>Releases the single-process writer lease.</summary>
    public void Dispose()
    {
        foreach (var writer in writers.Values) writer.Dispose();
        writers.Clear();
        lease.Dispose();
    }
}
