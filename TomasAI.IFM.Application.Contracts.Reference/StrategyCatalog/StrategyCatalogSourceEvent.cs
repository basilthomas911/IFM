using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Records a catalog transition together with its replayable version state.</summary>
[MessagePackObject]
public sealed record StrategyCatalogSourceEvent : IEvent<ActorEntityId>, IRequireDurableProjection
{
    bool IRequireDurableProjection.RequiresDurableProjection => !IsBaseline;
    DurableProjectionRequirement IRequireDurableProjection.RequiredProjection =>
        new("StrategyCatalogCommandActor", "StrategyCatalogEventProjector",
            TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.ApplyProjection);
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public string RequestJson { get; init; } = string.Empty;
    [Key(9)] public string BeforeJson { get; init; } = string.Empty;
    [Key(10)] public string AfterJson { get; init; } = string.Empty;
    [Key(11)] public string Principal { get; init; } = string.Empty;
    [Key(12)] public bool IsBaseline { get; init; }

    [IgnoreMember] public string UserName => Principal;
    [IgnoreMember] public string EventName => nameof(StrategyCatalogSourceEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
