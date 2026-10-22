using MessagePack;
using System.Security.Cryptography;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
namespace TomasAI.IFM.Application.MarketData.OptionChainCache;
[MessagePackObject]
public sealed record OptionChainPublicationFrame([property: Key(0)] Guid GenerationId, [property: Key(1)] string Scope,
    [property: Key(2)] long Version, [property: Key(3)] int Length, [property: Key(4)] int Offset,
    [property: Key(5)] byte[] Bytes, [property: Key(6)] string Digest)
{
    public const int ChunkSize = 64 * 1024;
    public const int MaximumLength = 8 * 1024 * 1024;
    public static OptionChainPublicationFrame[] Encode(OptionChainPublication publication)
    {
        var bytes = MarketDataHostConnection.Encode(publication);
        if (bytes.Length > MaximumLength) throw new InvalidDataException("Prepared option publication exceeds its bounded transport size.");
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        return Enumerable.Range(0, (bytes.Length + ChunkSize - 1) / ChunkSize).Select(index => {
            var offset = index * ChunkSize;
            return new OptionChainPublicationFrame(publication.Snapshot.GenerationId, publication.LookupScope, publication.Version,
                bytes.Length, offset, bytes.AsSpan(offset, Math.Min(ChunkSize, bytes.Length - offset)).ToArray(), digest);
        }).ToArray();
    }
}
/// <summary>One bounded in-flight publication; missing chunks are repaired by the next periodic replay.</summary>
public sealed class OptionChainPublicationAssembler
{
    OptionChainPublicationFrame? identity;
    byte[]? buffer;
    int next;
    public OptionChainPublication? Accept(OptionChainPublicationFrame frame)
    {
        if (frame.Length is < 1 or > OptionChainPublicationFrame.MaximumLength || frame.Bytes.Length is < 1 or > OptionChainPublicationFrame.ChunkSize
            || frame.Offset < 0 || frame.Offset > frame.Length - frame.Bytes.Length || frame.Scope.Length > 512 || frame.Digest.Length != 64) return null;
        if (frame.Offset == 0) { identity = frame; buffer = new byte[frame.Length]; next = 0; }
        if (identity is null || buffer is null || next != frame.Offset || identity.GenerationId != frame.GenerationId
            || identity.Version != frame.Version || identity.Scope != frame.Scope || identity.Length != frame.Length || identity.Digest != frame.Digest) return null;
        frame.Bytes.CopyTo(buffer, next); next += frame.Bytes.Length;
        if (next != buffer.Length) return null;
        var complete = buffer; buffer = null; identity = null;
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(complete)), frame.Digest, StringComparison.Ordinal)) return null;
        var publication = MarketDataHostConnection.Decode<OptionChainPublication>(complete);
        return publication.Snapshot.GenerationId == frame.GenerationId && publication.LookupScope == frame.Scope && publication.Version == frame.Version ? publication : null;
    }
}
