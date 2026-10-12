using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>Provides one normalized trade retained only for exact session-state recovery.</summary>
[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct FuturesTradeReplayObservation(
    [property: Key(0)] decimal Price,
    [property: Key(1)] uint Size,
    [property: Key(2)] long SourceSequence,
    [property: Key(3)] DateTimeOffset EventTimestampUtc,
    [property: Key(4)] NormalizedTradeAction Action,
    [property: Key(5)] NormalizedTradeConditionFlags Conditions);
