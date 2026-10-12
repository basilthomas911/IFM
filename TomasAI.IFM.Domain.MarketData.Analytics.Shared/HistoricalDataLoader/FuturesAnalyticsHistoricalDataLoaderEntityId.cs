using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Identifies one durable futures Analytics historical data-load attempt.</summary>
[MessagePackObject]
public readonly record struct FuturesAnalyticsHistoricalDataLoaderEntityId(
    [property: Key(0)] Guid Value) : IActorEntityId
{
    /// <summary>Formats the attempt identity for actor routing.</summary>
    public string Format() => Value.ToString("N");
}
