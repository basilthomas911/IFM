using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

/// <summary>Side-effect-free, generation-correlated NATS command for downstream recovery proof.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record RecoveryCanaryCommand : ICommand<ActorEntityId>
{
    public const string Actor = "SupervisorRecoveryCanary";
    public const string Verb = "Probe";
    public const int ErrorId = 9710;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.SupervisorBoundedContext;
    [Key(6)] public Guid CorrelationId { get; init; }
    [Key(7)] public Guid GenerationId { get; init; }
    [Key(8)] public DateOnly ValueDate { get; init; }
    [Key(9)] public string Dataset { get; init; } = string.Empty;
    [Key(10)] public DateTime IssuedUtc { get; init; }
    [IgnoreMember] public string CommandName => nameof(RecoveryCanaryCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;

    /// <summary>Creates a canary command for serialization and object-initializer callers.</summary>
    public RecoveryCanaryCommand() { }

    /// <summary>Creates a generation-correlated recovery proof command.</summary>
    /// <param name="commandId">The permanent command identifier.</param>
    /// <param name="subject">The correlation-routed canary subject.</param>
    /// <param name="correlationId">The proof correlation identity.</param>
    /// <param name="generationId">The publisher generation identity.</param>
    /// <param name="valueDate">The trading value date.</param>
    /// <param name="dataset">The Databento dataset.</param>
    /// <param name="issuedUtc">The proof creation time.</param>
    public RecoveryCanaryCommand(Guid commandId, ActorSubject subject, Guid correlationId,
        Guid generationId, DateOnly valueDate, string dataset, DateTime issuedUtc)
    {
        CommandId = commandId;
        Subject = subject;
        CorrelationId = correlationId;
        GenerationId = generationId;
        ValueDate = valueDate;
        Dataset = dataset;
        IssuedUtc = issuedUtc;
    }

    /// <summary>Reconstructs every permanent MessagePack field in numeric key order.</summary>
    /// <param name=commandId>The permanent command identifier.</param>
    /// <param name=subject>The routed actor subject.</param>
    /// <param name=postEvents>Whether generated events are posted.</param>
    /// <param name=entityId>The Supervisor entity identity.</param>
    /// <param name=errorCode>The command error code.</param>
    /// <param name=routeTo>The destination bounded context.</param>
    /// <param name=correlationId>The proof correlation identifier.</param>
    /// <param name=generationId>The publisher generation identifier.</param>
    /// <param name=valueDate>The trading value date.</param>
    /// <param name=dataset>The Databento dataset.</param>
    /// <param name=issuedUtc>The proof creation time.</param>
    [SerializationConstructor]
    public RecoveryCanaryCommand(Guid commandId, ActorSubject subject, bool postEvents,
        ActorEntityId entityId, int errorCode, BoundedContextName routeTo,
        Guid correlationId, Guid generationId, DateOnly valueDate, string dataset, DateTime issuedUtc)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        CorrelationId = correlationId;
        GenerationId = generationId;
        ValueDate = valueDate;
        Dataset = dataset;
        IssuedUtc = issuedUtc;
    }
}
