using MessagePack;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Commands;

/// <summary>Requests one authorized, audited, generation-fenced entity-mailbox lifecycle operation.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteSupervisorActorOperationCommand : ICommand<ActorEntityId>
{
    public const string Actor = "SupervisorCommand";
    public const string Verb = "ExecuteActorOperation";
    public const int ErrorId = 9701;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(3)] public ActorThreadId Target { get; init; }
    [Key(4)] public long ExpectedGeneration { get; init; }
    [Key(5)] public SupervisorActorOperationKind Operation { get; init; }
    [Key(6)] public string Requester { get; init; } = string.Empty;
    [Key(7)] public string Reason { get; init; } = string.Empty;
    [Key(8)] public long TimeoutTicks { get; init; }

    [IgnoreMember] public string CommandName => nameof(ExecuteSupervisorActorOperationCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.SupervisorBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => "SupervisorCommandActor";
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public TimeSpan Timeout => TimeSpan.FromTicks(TimeoutTicks);

    public ExecuteSupervisorActorOperationCommand() { }

    public ExecuteSupervisorActorOperationCommand(Guid commandId, ActorSubject subject, ActorEntityId entityId,
        ActorThreadId target, long expectedGeneration, SupervisorActorOperationKind operation,
        string requester, string reason, long timeoutTicks)
    {
        CommandId = commandId;
        Subject = subject;
        EntityId = entityId;
        Target = target;
        ExpectedGeneration = expectedGeneration;
        Operation = operation;
        Requester = requester;
        Reason = reason;
        TimeoutTicks = timeoutTicks;
    }
}
