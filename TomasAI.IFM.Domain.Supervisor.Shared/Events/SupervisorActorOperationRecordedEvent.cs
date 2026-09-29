using MessagePack;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Events;

/// <summary>Immutable audit event emitted after a Supervisor lifecycle command reaches a terminal outcome.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record SupervisorActorOperationRecordedEvent : IEvent<ActorEntityId>
{
    public const string Actor = "SupervisorEvent";
    public const string Verb = "ActorOperationRecorded";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = "SupervisorCommandActor";
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ActorThreadId Target { get; init; }
    [Key(9)] public long ExpectedGeneration { get; init; }
    [Key(10)] public SupervisorActorOperationKind Operation { get; init; }
    [Key(11)] public string Requester { get; init; } = string.Empty;
    [Key(12)] public string Reason { get; init; } = string.Empty;
    [Key(13)] public long TimeoutTicks { get; init; }
    [Key(14)] public SupervisorOperationOutcome Outcome { get; init; }
    [Key(15)] public string Stage { get; init; } = string.Empty;
    [Key(16)] public string FailureReason { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => Requester;
    [IgnoreMember] public string EventName => nameof(SupervisorActorOperationRecordedEvent);
    [IgnoreMember] public EventType EventType => EventType.ServiceApiEvent;
}
