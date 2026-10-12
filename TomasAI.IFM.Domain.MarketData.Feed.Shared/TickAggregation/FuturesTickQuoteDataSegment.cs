using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "Preserves the existing externally referenced identity and named API contract.")]
[MessagePackFormatter(typeof(FuturesTickQuoteDataSegmentFormatter))]
public readonly struct FuturesTickQuoteDataSegment : IDisposable
{
    public const ushort MaximumCount = 4096;

    public FuturesTickQuoteDataSegment(FuturesTickQuoteData[] buffer, ushort count)
        : this(buffer, count, null)
    {
    }

    internal FuturesTickQuoteDataSegment(
        FuturesTickQuoteData[] buffer,
        ushort count,
        IDisposable? owner)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count is 0 or > MaximumCount || count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        Buffer = buffer;
        Count = count;
        Owner = owner;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    public FuturesTickQuoteData[] Buffer { get; }
    public ushort Count { get; }
    public ReadOnlySpan<FuturesTickQuoteData> Items => Buffer.AsSpan(0, Count);
    [IgnoreMember] internal IDisposable? Owner { get; }

    /// <summary>Returns a decoded quote slot after its final consumer finishes.</summary>
    public void Dispose() => Owner?.Dispose();
}
