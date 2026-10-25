using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Historical;

/// <summary>Provides one provider-neutral decoded historical record.</summary>
public sealed record HistoricalProviderRecord
{
    /// <summary>Gets the normalized record kind.</summary>
    public required HistoricalProviderRecordKind Kind { get; init; }
    /// <summary>Gets the provider symbol.</summary>
    public required string Symbol { get; init; }
    /// <summary>Gets the provider instrument identity.</summary>
    public required string InstrumentId { get; init; }
    /// <summary>Gets the provider publisher identity.</summary>
    public string PublisherId { get; init; } = string.Empty;
    /// <summary>Gets the exchange event time in UTC.</summary>
    public required DateTimeOffset EventTimestampUtc { get; init; }
    /// <summary>Gets the source sequence when supplied.</summary>
    public long SourceSequence { get; init; }
    /// <summary>Gets the open price for OHLCV records.</summary>
    public decimal Open { get; init; }
    /// <summary>Gets the high price for OHLCV records.</summary>
    public decimal High { get; init; }
    /// <summary>Gets the low price for OHLCV records.</summary>
    public decimal Low { get; init; }
    /// <summary>Gets the close or individual trade price.</summary>
    public decimal CloseOrPrice { get; init; }
    /// <summary>Gets OHLCV volume or an individual trade size.</summary>
    public long VolumeOrSize { get; init; }
    /// <summary>Gets the normalized provider-neutral action.</summary>
    public NormalizedTradeAction Action { get; init; }
    /// <summary>Gets the normalized provider-neutral side.</summary>
    public NormalizedTradeSide Side { get; init; }
    /// <summary>Gets normalized provider-neutral conditions.</summary>
    public NormalizedTradeConditionFlags Conditions { get; init; }
}
