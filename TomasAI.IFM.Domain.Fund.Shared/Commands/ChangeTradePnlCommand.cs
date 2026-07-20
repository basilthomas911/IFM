using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to change the profit and loss (PnL) of a trade within a specific fund.
/// </summary>
/// <remarks>
/// This command carries a <see cref="FundTransactionReadModel" /> representing a PnL-impacting transaction.
/// It is routed to the Fund bounded context and targets the fund identified by the transaction's <c>FundId</c>.
/// MessagePack serialization uses numeric keys (0-5 from base command and6 for the transaction).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeTradePnlCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "ChangeTradePnl";

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FundId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Gets or sets the fund transaction (PnL data) associated with this command.
    /// </summary>
    /// <remarks>Serialized with key6. Base command reserves keys0-5.</remarks>
    [Key(6)]
    public FundTransactionReadModel FundTransaction { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public ChangeTradePnlCommand() { }

    /// <summary>
    /// Convenience constructor for creating the command in application code.
    /// </summary>
    /// <param name="fundTransaction">The transaction whose PnL data is to be processed.</param>
    public ChangeTradePnlCommand(FundTransactionReadModel fundTransaction)
    {
        FundTransaction = fundTransaction ?? throw new ArgumentNullException(nameof(fundTransaction));
        EntityId = new(fundTransaction.FundId);
        ErrorCode = 2005;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters must align with key order0..6.
    /// </summary>
    /// <param name="commandId">Command identifier (key0).</param>
    /// <param name="subject">Actor subject for routing (key1).</param>
    /// <param name="postEvents">Flag indicating if resulting events should be posted (key2).</param>
    /// <param name="entityId">Fund entity identifier (key3).</param>
    /// <param name="errorCode">Associated error code (key4).</param>
    /// <param name="routeTo">Target bounded context (key5).</param>
    /// <param name="fundTransaction">Fund transaction payload (key6).</param>
    [SerializationConstructor]
    public ChangeTradePnlCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundId entityId,
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
