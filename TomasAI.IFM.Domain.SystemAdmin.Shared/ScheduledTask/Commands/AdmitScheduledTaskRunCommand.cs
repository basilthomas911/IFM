using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;

/// <summary>Requests the concrete AdmitScheduledTaskRun business operation.</summary>
[MessagePackObject]
public sealed record AdmitScheduledTaskRunCommand : ICommand<ScheduledTaskId>
{
    public const string Actor = "ScheduledTaskCommand";
    public const string Verb = "AdmitScheduledTaskRun";
    [Key(0)] public Guid CommandId { get; init; } = default;
    [Key(1)] public ActorSubject Subject { get; init; } = default!;
    [Key(2)] public ScheduledTaskId EntityId { get; init; } = default;
    [Key(3)] public long ExpectedRevision { get; init; } = 0;
    [Key(4)] public string Operator { get; init; } = "";
    [Key(5)] public string Reason { get; init; } = "";
    [Key(6)] public Guid OperationCommandId { get; init; } = default;
    [Key(7)] public Guid RunId { get; init; } = default;
    [Key(8)] public long DefinitionRevision { get; init; } = 0;
    [Key(9)] public DateTimeOffset IntendedFireTimeUtc { get; init; } = default;
    [Key(10)] public bool Manual { get; init; } = false;
    [Key(11)] public string HostId { get; init; } = "";
    [Key(12)] public string Environment { get; init; } = "";
    [IgnoreMember] public string CommandName => nameof(AdmitScheduledTaskRunCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 9300;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.SystemAdminBoundedContext;
}
