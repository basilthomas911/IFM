using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;

/// <summary>Requests the concrete ChangeScheduledTaskSchedule business operation.</summary>
[MessagePackObject]
public sealed record ChangeScheduledTaskScheduleCommand : ICommand<ScheduledTaskId>
{
    public const string Actor = "ScheduledTaskCommand";
    public const string Verb = "ChangeScheduledTaskSchedule";
    [Key(0)] public Guid CommandId { get; init; } = default;
    [Key(1)] public ActorSubject Subject { get; init; } = default!;
    [Key(2)] public ScheduledTaskId EntityId { get; init; } = default;
    [Key(3)] public long ExpectedRevision { get; init; } = 0;
    [Key(4)] public string Operator { get; init; } = "";
    [Key(5)] public string Reason { get; init; } = "";
    [Key(6)] public Guid OperationCommandId { get; init; } = default;
    [Key(7)] public ScheduledTaskSchedule Schedule { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(ChangeScheduledTaskScheduleCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 9300;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.SystemAdminBoundedContext;
}
