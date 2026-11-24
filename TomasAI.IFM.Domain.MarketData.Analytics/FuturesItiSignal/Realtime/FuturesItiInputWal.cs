using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Crash-safe local staging before queue admission. Segments are reclaimed only after the shared journal commits.</summary>
public sealed class FuturesItiInputWal : IAsyncDisposable
{
    const long SegmentBytes = 16 * 1024 * 1024;
    const int MaxPayload = 60 * 1024;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly string root;
    FileStream writer;
    long segment;
    public Guid RootId { get; }

    public FuturesItiInputWal(string directory)
    {
        root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, "journal.id");
        if (!File.Exists(marker))
        {
            using var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(Guid.NewGuid().ToByteArray());
            file.Flush(true);
        }
        var id = File.ReadAllBytes(marker);
        if (id.Length != 16) throw new InvalidDataException("Invalid ITI journal-root identity.");
        RootId = new(id);
        segment = Directory.EnumerateFiles(root, "*.wal").Select(path => Parse(path)).DefaultIfEmpty(1).Max();
        writer = Open(segment);
    }

    string PathFor(long value) => Path.Combine(root, value.ToString("D16", System.Globalization.CultureInfo.InvariantCulture) + ".wal");
    static long Parse(string path) => long.Parse(Path.GetFileNameWithoutExtension(path), System.Globalization.CultureInfo.InvariantCulture);
    FileStream Open(long value) => new(PathFor(value), FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan);

    public async ValueTask<Position> AppendAsync(byte[] payload, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length is < 1 or > MaxPayload) throw new InvalidDataException("ITI journal input exceeds its frame bound.");
        await gate.WaitAsync(token).ConfigureAwait(false);
        var buffer = ArrayPool<byte>.Shared.Rent(payload.Length + 36);
        try
        {
            if (writer.Length >= SegmentBytes)
            {
                await writer.DisposeAsync().ConfigureAwait(false);
                writer = Open(++segment);
            }
            BinaryPrimitives.WriteInt32LittleEndian(buffer, payload.Length);
            SHA256.HashData(payload, buffer.AsSpan(4,32));
            payload.CopyTo(buffer, 36);
            writer.Write(buffer, 0, payload.Length + 36);
            writer.Flush(true); // Admission follows the durable flush, not just an OS-buffer write.
            return new(segment, writer.Position);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); gate.Release(); }
    }

    public async IAsyncEnumerable<Record> ReplayAsync([EnumeratorCancellation] CancellationToken token = default)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.wal").OrderBy(Parse))
        {
            await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var header = new byte[36];
            while (reader.Position < reader.Length)
            {
                await reader.ReadExactlyAsync(header, token).ConfigureAwait(false);
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length is < 1 or > MaxPayload) throw new InvalidDataException("Corrupt ITI WAL length; authentic source recovery is required.");
                var payload = new byte[length];
                await reader.ReadExactlyAsync(payload, token).ConfigureAwait(false);
                if (!SHA256.HashData(payload).AsSpan().SequenceEqual(header.AsSpan(4,32)))
                    throw new InvalidDataException("Corrupt ITI WAL checksum; authentic source recovery is required.");
                yield return new(new(Parse(path), reader.Position), payload);
            }
        }
    }

    /// <summary>Only a confirmed shared-journal commit can acknowledge local staging.</summary>
    public async ValueTask AcknowledgeAsync(Position position)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.wal"))
            {
                var value = Parse(path);
                if (value < segment && (value < position.Segment || value == position.Segment && position.EndOffset == new FileInfo(path).Length))
                {
                    var target = Path.GetFullPath(path);
                    if (!string.Equals(Path.GetDirectoryName(target), root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("ITI WAL target escaped its journal directory.");
                    File.Delete(target);
                }
            }
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { await writer.DisposeAsync().ConfigureAwait(false); }
        finally { gate.Release(); gate.Dispose(); }
    }

    public readonly record struct Position(long Segment, long EndOffset);
    public sealed record Record(Position Position, byte[] Payload);
}
