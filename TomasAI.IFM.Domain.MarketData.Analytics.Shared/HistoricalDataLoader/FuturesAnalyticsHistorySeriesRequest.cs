using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Defines one provider-neutral series in a data load request.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistorySeriesRequest
{
    /// <summary>Gets the continuation-series or exact-contract identity.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }
    /// <summary>Gets an optional explicit provider contract/symbol.</summary>
    [Key(1)] public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the requested historical record shape.</summary>
    [Key(2)] public FuturesAnalyticsHistoricalSchema Schema { get; init; }
    /// <summary>Gets whether exact trades are mandatory for this series.</summary>
    [Key(3)] public bool ExactTradesRequired { get; init; }
}
