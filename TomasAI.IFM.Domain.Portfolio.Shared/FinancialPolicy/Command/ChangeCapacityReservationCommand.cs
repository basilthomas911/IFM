using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Change through the CapacityReservationCommand actor; retries preserve operation identity and semantic input hash.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ChangeCapacityReservationCommand : ICommand<CapacityReservationEntityId>, IFinancialRequest<CapacityLifecycleRequest>
{
    /// <summary>Creates an empty published message for serialization and existing callers.</summary>
    public ChangeCapacityReservationCommand() { }

    /// <summary>Rehydrates every published legacy command field in permanent numeric-key order.</summary>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="operationId">The OperationId field.</param>
    /// <param name="portfolioId">The PortfolioId field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="causationId">The CausationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    /// <param name="expiresAtUtc">The ExpiresAtUtc field.</param>
    /// <param name="expectedFinancialRevision">The ExpectedFinancialRevision field.</param>
    /// <param name="body">The Body field.</param>
    /// <param name="inputSha256">The InputSha256 field.</param>
    /// <param name="access">The Access field.</param>
    [SerializationConstructor]
    public ChangeCapacityReservationCommand(int schemaVersion, Guid commandId, ActorSubject subject, bool postEvents, CapacityReservationEntityId entityId, int errorCode, BoundedContextName routeTo, Guid operationId, int portfolioId, Guid correlationId, Guid causationId, DateTime requestedAtUtc, DateTime expiresAtUtc, long expectedFinancialRevision, CapacityLifecycleRequest body, string inputSha256, FinancialAccess access)
    {
        SchemaVersion = schemaVersion;
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        OperationId = operationId;
        PortfolioId = portfolioId;
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestedAtUtc = requestedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ExpectedFinancialRevision = expectedFinancialRevision;
        Body = body;
        InputSha256 = inputSha256;
        Access = access;
    }
    public const string Actor = "CapacityReservationCommand";
    public const string Verb = "Change";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Command, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = true;
    [Key(4)] public CapacityReservationEntityId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(5)] public int ErrorCode { get; init; } = 34124;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.CapacityReservationBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public CapacityLifecycleRequest Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(ChangeCapacityReservationCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}
