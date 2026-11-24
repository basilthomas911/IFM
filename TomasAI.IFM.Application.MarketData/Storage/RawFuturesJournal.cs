using System.Buffers.Binary;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Storage;

/// <summary>Reads the native IFMRAW01 journal. Partial active writes wait; damaged retired tails block recovery.</summary>
public static class RawFuturesJournal
{
    public sealed record Cursor(Guid Epoch, long Offset, long Ordinal);
    public static bool OwnerIsActive(string path)
    {
        var ownerPath = Path.ChangeExtension(path, ".owner");
        if (!File.Exists(ownerPath)) throw new InvalidDataException("Raw journal ownership evidence is missing.");
        try
        {
            using var owner = new FileStream(ownerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false; // FileShare.None also takes the Unix flock used by Rust File::try_lock.
        }
        catch (IOException) { return true; }
    }
    public static Cursor LoadCursor(string path, Guid epoch)
    {
        var file = path + ".cursor";
        if (!File.Exists(file)) return new(epoch, 24, 0);
        var bytes = File.ReadAllBytes(file);
        if (bytes.Length != 64 || new Guid(bytes.AsSpan(0, 16)) != epoch
            || !SHA256.HashData(bytes.AsSpan(0, 32)).AsSpan().SequenceEqual(bytes.AsSpan(32)))
            throw new InvalidDataException("Raw storage cursor is damaged or belongs to another source.");
        var offset = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16));
        var ordinal = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(24));
        if (offset < 24 || ordinal < 0) throw new InvalidDataException("Invalid raw storage cursor.");
        return new(epoch, offset, ordinal);
    }
    public static void CommitCursor(string path, Cursor cursor)
    {
        Span<byte> bytes = stackalloc byte[64]; cursor.Epoch.TryWriteBytes(bytes);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[16..], cursor.Offset);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[24..], cursor.Ordinal);
        SHA256.HashData(bytes[..32], bytes[32..]);
        var temporary = path + ".cursor.pending";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { file.Write(bytes); file.Flush(flushToDisk: true); }
        File.Move(temporary, path + ".cursor", overwrite: true);
    }
    public static async Task<(RawFuturesStorageBatch? Batch, bool Complete)> ReadAsync(string path, int count, CancellationToken token)
    {
        if (count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(count));
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var header = new byte[24];
        if (file.Length < 24)
        {
            if (!OwnerIsActive(path)) throw new InvalidDataException("Retired raw journal has an incomplete header.");
            return (null, false);
        }
        await file.ReadExactlyAsync(header, token).ConfigureAwait(false);
        if (!header.AsSpan(0, 8).SequenceEqual("IFMRAW01"u8)) throw new InvalidDataException("Unknown raw source journal version.");
        var epoch = new Guid(header.AsSpan(8));
        var cursor = LoadCursor(path, epoch);
        if (cursor.Offset > file.Length) throw new InvalidDataException("Raw storage cursor is beyond the journal tail.");
        file.Position = cursor.Offset;
        var records = new List<RawFuturesStorageRecord>(count);
        var next = cursor.Offset; var ordinal = cursor.Ordinal; var bytes = 0; var complete = false;
        var lengthBytes = new byte[4];
        while (records.Count < count)
        {
            var available = file.Length - file.Position;
            if (available < 4)
            {
                if (!OwnerIsActive(path))
                {
                    if (available != 0) throw new InvalidDataException("Retired raw journal has a partial record header.");
                    complete = true;
                }
                break;
            }
            await file.ReadExactlyAsync(lengthBytes, token).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if (length == 0) { complete = true; break; }
            if (length is < 26 or > 16_409) throw new InvalidDataException("Invalid raw observation length.");
            if (bytes + length > 56_000) break;
            if (file.Length - file.Position < length + 32)
            {
                if (!OwnerIsActive(path)) throw new InvalidDataException("Retired raw journal has an incomplete observation.");
                break;
            }
            var record = new byte[length]; var digest = new byte[32];
            await file.ReadExactlyAsync(record, token).ConfigureAwait(false);
            await file.ReadExactlyAsync(digest, token).ConfigureAwait(false);
            if (!SHA256.HashData(record).AsSpan().SequenceEqual(digest)) throw new InvalidDataException("Raw journal observation checksum mismatch.");
            var accepted = BinaryPrimitives.ReadInt64LittleEndian(record.AsSpan(17));
            if (accepted != ordinal + 1 || record[0] is not (1 or 2)) throw new InvalidDataException("Raw storage source sequence gap.");
            records.Add(new(accepted, new Guid(record.AsSpan(1, 16)), record[0], record.AsSpan(25).ToArray()));
            bytes += length; ordinal = accepted; next = file.Position;
        }
        return (records.Count == 0 ? null : new(epoch, next, records.ToArray()), complete);
    }
    public static void RemoveCompleted(string path)
    {
        File.Delete(path);
        File.Delete(path + ".cursor");
        File.Delete(Path.ChangeExtension(path, ".owner"));
    }
}
