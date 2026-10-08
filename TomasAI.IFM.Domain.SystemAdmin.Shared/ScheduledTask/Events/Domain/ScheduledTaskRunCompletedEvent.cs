using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;

/// <summary>Persists the ScheduledTaskRunCompleted source outcome.</summary>
[MessagePackObject]
public sealed record ScheduledTaskRunCompletedEvent : IEvent<ScheduledTaskId>
{
    public const string Actor = "ScheduledTaskEvent";
    public const string Verb = "ScheduledTaskRunCompleted";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public Guid Id { get; init; } = Guid.NewGuid();
    [Key(2)] public long EventId { get; init; } = 0;
    [Key(3)] public Guid CommandId { get; init; } = default;
    [Key(4)] public ScheduledTaskId EntityId { get; init; } = default;
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "";
    [Key(7)] public DateTime ReceivedOn { get; init; } = DateTime.UtcNow;
    [Key(8)] public Guid OperationCommandId { get; init; } = default;
    [Key(9)] public ScheduledTaskRun? ScheduledTaskRun { get; init; } = null;
    [IgnoreMember] public string EventName => nameof(ScheduledTaskRunCompletedEvent);
    [IgnoreMember] public string UserName => "SystemAdmin";
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
