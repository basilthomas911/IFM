using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>Publishes a completed provider definition catalog and its immutable evidence snapshot.</summary>
[MessagePackObject]
public sealed record InstrumentDefinitionCatalogPublishedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public DateTime CompletedUtc { get; init; }
    [Key(8)] public long RecordCount { get; init; }
    [Key(9)] public string[] Datasets { get; init; } = [];
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(InstrumentDefinitionCatalogPublishedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
