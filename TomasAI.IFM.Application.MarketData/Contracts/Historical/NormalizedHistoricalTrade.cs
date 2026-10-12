using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Provides one normalized historical trade for exact replay.</summary>
public sealed record NormalizedHistoricalTrade
{
    /// <summary>Gets the actual domain contract identity.</summary>
    public required string ContractId { get; init; }
    /// <summary>Gets the futures value date.</summary>
    public required DateOnly ValueDate { get; init; }
    /// <summary>Gets the normalized price.</summary>
    public required decimal Price { get; init; }
    /// <summary>Gets the individual executed size.</summary>
    public required long Size { get; init; }
    /// <summary>Gets the exchange event time.</summary>
    public required DateTimeOffset EventTimestampUtc { get; init; }
    /// <summary>Gets the source sequence.</summary>
    public long SourceSequence { get; init; }
    /// <summary>Gets the normalized action.</summary>
    public NormalizedTradeAction Action { get; init; }
    /// <summary>Gets the normalized aggressor side.</summary>
    public NormalizedTradeSide Side { get; init; }
    /// <summary>Gets provider-neutral conditions.</summary>
    public NormalizedTradeConditionFlags Conditions { get; init; }
    /// <summary>Gets the provider instrument identity.</summary>
    public required string ProviderInstrumentId { get; init; }
}
