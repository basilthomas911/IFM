using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>Cumulative session extrema and final authentic trade; no raw history is replaced by this view.</summary>
[MessagePackObject]
public sealed record FuturesEodTradeBatchRealtimeEvent : IEvent<FuturesEodDataId>
{
    public const string Actor = "FuturesEodDataEvent";
    public const string Verb = "CumulativeTradeBatch";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesEodDataId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "RawFuturesEodCoalescer";
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public decimal High { get; init; }
    [Key(9)] public decimal Low { get; init; }
    [Key(10)] public FuturesTickTradeDataChangedEvent Last { get; init; } = new();
    [IgnoreMember] public string UserName => "";
    [IgnoreMember] public string EventName => nameof(FuturesEodTradeBatchRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
