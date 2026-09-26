using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Submits one exact consumed order to the durable internal emulator. Never routes to a live broker.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record SubmitEmulatorOrderCommand : ICommand<LedgerPortfolioId>, IFinancialRequest<SubmitEmulatorOrderRequest>
{
    /// <summary>Creates an empty published message for serialization and existing callers.</summary>
    public SubmitEmulatorOrderCommand() { }

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
    public SubmitEmulatorOrderCommand(int schemaVersion, Guid commandId, ActorSubject subject, bool postEvents, LedgerPortfolioId entityId, int errorCode, BoundedContextName routeTo, Guid operationId, int portfolioId, Guid correlationId, Guid causationId, DateTime requestedAtUtc, DateTime expiresAtUtc, long expectedFinancialRevision, SubmitEmulatorOrderRequest body, string inputSha256, FinancialAccess access)
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
    public const string Actor = "EmulatorExecutionCommand";
    public const string Verb = "Submit";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Command, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = true;
    [Key(4)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(5)] public int ErrorCode { get; init; } = 34126;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.CapacityReservationBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public SubmitEmulatorOrderRequest Body { get; init; } = new(new(), Guid.Empty, Guid.Empty, string.Empty);
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(SubmitEmulatorOrderCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}
