namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>Original provider trade fields used by application cache initialization.</summary>
public sealed record BootstrapTradeRow(DateOnly ValueDate, decimal Price, long Size, long Sequence,
    long EventNanoseconds, long ReceiveNanoseconds, short Action, short HeaderFlags, uint InstrumentId, ushort PublisherId)
{
    /// <summary>Clustering time used to select this raw trade, when read from a bounded window.</summary>
    public TimeOnly? AggregationTimeUtc { get; init; }
}
