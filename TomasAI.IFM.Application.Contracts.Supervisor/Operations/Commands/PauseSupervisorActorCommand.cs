using MessagePack;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;

/// <summary>Requests the Supervisor to pause one actor mailbox.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PauseSupervisorActorCommand : ISupervisorOperationCommand
{
    public const string Actor = "SupervisorCommand";
    public const string Verb = "PauseActor";
    public const int ErrorId = 9711;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.SupervisorBoundedContext;
    [Key(6)] public ActorThreadId Target { get; init; }
    [Key(7)] public long ExpectedGeneration { get; init; }
    [Key(8)] public string Requester { get; init; } = string.Empty;
    [Key(9)] public string Reason { get; init; } = string.Empty;
    [Key(10)] public long TimeoutTicks { get; init; }
    [IgnoreMember] public SupervisorActorOperationKind OperationKind => SupervisorActorOperationKind.Pause;
    [IgnoreMember] public string CommandName => nameof(PauseSupervisorActorCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => "SupervisorCommandActor";
    [IgnoreMember] public TimeSpan Timeout => TimeSpan.FromTicks(TimeoutTicks);

    /// <summary>Creates a command for serialization and object-initializer callers.</summary>
    public PauseSupervisorActorCommand() { }

    /// <summary>Creates a routed Supervisor operation request.</summary>
    /// <param name="commandId">The permanent command identifier.</param>
    /// <param name="subject">The Supervisor actor subject.</param>
    /// <param name="target">The target actor thread.</param>
    /// <param name="expectedGeneration">The expected target generation.</param>
    /// <param name="requester">The requesting operator.</param>
    /// <param name="reason">The operator's reason.</param>
    /// <param name="timeoutTicks">The bounded operation deadline in ticks.</param>
    public PauseSupervisorActorCommand(Guid commandId, ActorSubject subject, ActorThreadId target,
        long expectedGeneration, string requester, string reason, long timeoutTicks)
    {
        CommandId = commandId;
        Subject = subject;
        Target = target;
        ExpectedGeneration = expectedGeneration;
        Requester = requester;
        Reason = reason;
        TimeoutTicks = timeoutTicks;
    }

    /// <summary>Reconstructs every permanent MessagePack field in numeric key order.</summary>
    /// <param name="commandId">The permanent command identifier.</param>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="postEvents">Whether generated events are posted.</param>
    /// <param name="entityId">The Supervisor entity identity.</param>
    /// <param name="errorCode">The command error code.</param>
    /// <param name="routeTo">The destination bounded context.</param>
    /// <param name="target">The target actor thread.</param>
    /// <param name="expectedGeneration">The expected target generation.</param>
    /// <param name="requester">The requesting operator.</param>
    /// <param name="reason">The operator's reason.</param>
    /// <param name="timeoutTicks">The bounded operation deadline in ticks.</param>
    [SerializationConstructor]
    public PauseSupervisorActorCommand(Guid commandId, ActorSubject subject, bool postEvents,
        ActorEntityId entityId, int errorCode, BoundedContextName routeTo,
        ActorThreadId target, long expectedGeneration, string requester, string reason,
        long timeoutTicks)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Target = target;
        ExpectedGeneration = expectedGeneration;
        Requester = requester;
        Reason = reason;
        TimeoutTicks = timeoutTicks;
    }}
