using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>Marks the atomic capture of the PostgreSQL MarketData baseline.</summary>
[MessagePackObject]
public sealed record MarketDataReconciliationBaselineEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public int CalendarRows { get; init; }
    [Key(8)] public int YieldCurveRows { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(MarketDataReconciliationBaselineEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
