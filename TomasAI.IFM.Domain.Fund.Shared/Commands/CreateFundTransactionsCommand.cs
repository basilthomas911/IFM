using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to create one or more fund transactions in a single request.
/// </summary>
/// <remarks>
/// MessagePack-serializable:
/// - Base command members use keys 0�5 (from base command structure).
/// - Payload member (<see cref="FundTransactions" />) uses key 6.
/// The <see cref="EntityId" /> is derived from the first transaction (FundId, OrderId, ValueDate); if none exist,
/// a default (0,0, DateOnly.MinValue) identifier is used.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record CreateFundTransactionsCommand : ICommand<FundTransactionEntityId>
{
    public const string Actor = "FundTransactionCommand";
    public const string Verb = "CreateMultiple";
    public const int ErrorId = 2011;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FundTransactionEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Gets or sets the collection of fund transactions to create.
    /// </summary>
    /// <remarks>Serialized with key 6.</remarks>
    [Key(6)]
    public FundTransactionReadModel[] FundTransactions { get; init; } = Array.Empty<FundTransactionReadModel>();

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public CreateFundTransactionsCommand() { }

    /// <summary>
    /// Convenience constructor for application code.
    /// </summary>
    /// <param name="fundTransactions">Array of transactions to create.</param>
    public CreateFundTransactionsCommand(FundTransactionEntityId fundTransactionsId, FundTransactionReadModel[] fundTransactions)
    {
        FundTransactions = fundTransactions ?? throw new ArgumentNullException(nameof(fundTransactions));
        EntityId = fundTransactionsId;
        ErrorCode = 2011;
        RouteTo = BoundedContextName.FundTransactionBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor (parameters must align with key order 0�6).
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Whether resulting events should be posted (key 2).</param>
    /// <param name="entityId">Derived entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="fundTransactions">Transactions payload (key 6).</param>
    [SerializationConstructor]
    public CreateFundTransactionsCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundTransactionEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FundTransactionReadModel[] fundTransactions)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundTransactions = fundTransactions ?? [];
    }

}