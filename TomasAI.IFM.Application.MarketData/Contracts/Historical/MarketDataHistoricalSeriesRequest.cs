using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Describes one domain series acquisition profile.</summary>
public sealed record MarketDataHistoricalSeriesRequest
{
    /// <summary>Gets the provider-neutral series identity.</summary>
    public required MarketSeriesIdentity SeriesIdentity { get; init; }
    /// <summary>Gets the actual contract when an exact contract is requested.</summary>
    public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the provider-neutral schema.</summary>
    public required HistoricalDataSchema Schema { get; init; }
    /// <summary>Gets whether exact individual trades are required.</summary>
    public bool ExactTradesRequired { get; init; }
}
