using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;

/// <summary>Reports the ScheduledTaskCreated fail outcome.</summary>
[MessagePackObject]
public sealed record ScheduledTaskCreatedFailEvent : IErrorEvent<ScheduledTaskId>
{
    public const string Actor = "ScheduledTaskEvent";
    public const string Verb = "ScheduledTaskCreatedFail";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public Guid Id { get; init; } = Guid.NewGuid();
    [Key(2)] public long EventId { get; init; } = 0;
    [Key(3)] public Guid CommandId { get; init; } = default;
    [Key(4)] public ScheduledTaskId EntityId { get; init; } = default;
    [Key(5)] public string AggregateId { get; init; } = "";
    [Key(6)] public string EventSource { get; init; } = "";
    [Key(7)] public DateTime ReceivedOn { get; init; } = DateTime.UtcNow;
    [Key(8)] public Guid OperationCommandId { get; init; } = default;
    [Key(9)] public ScheduledTaskDefinition? ScheduledTaskDefinition { get; init; } = null;
    [Key(10)] public int ErrorCode { get; init; } = 9300;
    [Key(11)] public string ErrorMessage { get; init; } = "";
    [Key(12)] public ErrorType ErrorType { get; init; } = default;
    [Key(13)] public string ErrorData { get; init; } = "";
    [Key(14)] public string CommandName { get; init; } = "";
    [Key(15)] public string CommandData { get; init; } = "";
    [IgnoreMember] public string EventName => nameof(ScheduledTaskCreatedFailEvent);
    [IgnoreMember] public string UserName => "SystemAdmin";
    [IgnoreMember] public EventType EventType => EventType.ServiceEvent;
    [IgnoreMember] public DateTime ErrorDate => ReceivedOn;
}
