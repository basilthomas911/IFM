using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to create a new fund transaction.
/// </summary>
/// <remarks>
/// MessagePack-serializable using numeric keys:
/// - Base command members use keys 0�5.
/// - This payload member (<see cref="FundTransaction" />) uses key 6.
/// The command routes to <see cref="BoundedContextName.FundTransactionBoundedContext" /> and targets the
/// <see cref="FundTransactionEntityId" /> derived from the supplied transaction.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record CreateFundTransactionCommand : ICommand<FundTransactionEntityId>
{
    public const string Actor = "FundTransactionCommand";
    public const string Verb = "Create";
    public const int ErrorId = 2008;

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
    /// Gets or sets the fund transaction to be created.
    /// </summary>
    /// <remarks>Serialized with key 6.</remarks>
    [Key(6)]
    public FundTransactionReadModel FundTransaction { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public CreateFundTransactionCommand() { }

    /// <summary>
    /// Convenience constructor for application code.
    /// </summary>
    /// <param name="fundTransaction">The transaction to create.</param>
    public CreateFundTransactionCommand(FundTransactionReadModel fundTransaction)
    {
        FundTransaction = fundTransaction ?? throw new ArgumentNullException(nameof(fundTransaction));
        EntityId = fundTransaction.EntityId;
        ErrorCode = 2008;
        RouteTo = BoundedContextName.FundTransactionBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters must align with key order 0�6.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Whether resulting events should be posted (key 2).</param>
    /// <param name="entityId">Fund transaction entity identifier (key 3).</param>
    /// <param name="errorCode">Error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="fundTransaction">Fund transaction payload (key 6).</param>
    [SerializationConstructor]
    public CreateFundTransactionCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundTransactionEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FundTransactionReadModel fundTransaction)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundTransaction = fundTransaction;
    }
}