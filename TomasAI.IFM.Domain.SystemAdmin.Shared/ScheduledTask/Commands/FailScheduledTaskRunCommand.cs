using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;

/// <summary>Requests the concrete FailScheduledTaskRun business operation.</summary>
[MessagePackObject]
public sealed record FailScheduledTaskRunCommand : ICommand<ScheduledTaskId>
{
    public const string Actor = "ScheduledTaskRunCommand";
    public const string Verb = "FailScheduledTaskRun";
    [Key(0)] public Guid CommandId { get; init; } = default;
    [Key(1)] public ActorSubject Subject { get; init; } = default!;
    [Key(2)] public ScheduledTaskId EntityId { get; init; } = default;
    [Key(3)] public long ExpectedRevision { get; init; } = 0;
    [Key(4)] public string Operator { get; init; } = "";
    [Key(5)] public string Reason { get; init; } = "";
    [Key(6)] public Guid OperationCommandId { get; init; } = default;
    [Key(7)] public DateTimeOffset FinishedAtUtc { get; init; } = default;
    [Key(8)] public int? ExitCode { get; init; } = null;
    [Key(9)] public string Stage { get; init; } = "";
    [Key(10)] public string Detail { get; init; } = "";
    [Key(11)] public string StandardOutputTail { get; init; } = "";
    [Key(12)] public string StandardErrorTail { get; init; } = "";
    [Key(13)] public string OutputDirectory { get; init; } = "";
    [IgnoreMember] public string CommandName => nameof(FailScheduledTaskRunCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 9300;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.SystemAdminBoundedContext;
}
