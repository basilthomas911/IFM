using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to change the <see cref="TradeState" /> of a specific fund order trade.
/// </summary>
/// <remarks>
/// This command targets a single trade within a fund order and updates its state within the fund bounded context.
/// It carries routing metadata (inherited from base command structure) plus the trade identifier and
/// the new state. The command is MessagePack serializable using numeric keys for compact binary payloads.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeFundOrderTradeStateCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "ChangeFundOrderTradeState";
    public const int ErrorId = 2004;

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
    /// Gets or sets the identifier of the fund order trade whose state is to be changed.
    /// </summary>
    /// <remarks>Serialized with key 6 (base command reserves keys 0–5).</remarks>
    [Key(6)]
    public FundOrderTradeId FundOrderTradeId { get; init; }

    /// <summary>
    /// Gets or sets the new state to apply to the fund order trade.
    /// </summary>
    /// <remarks>Serialized with key 7.</remarks>
    [Key(7)]
    public TradeState TradeState { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public ChangeFundOrderTradeStateCommand() { }

    /// <summary>
    /// Convenience constructor for creating the command in application code.
    /// </summary>
    /// <param name="fundOrderTradeId">The target fund order trade identifier.</param>
    /// <param name="tradeState">The new trade state.</param>
    public ChangeFundOrderTradeStateCommand(FundOrderTradeId fundOrderTradeId, TradeState tradeState)
    {
        FundOrderTradeId = fundOrderTradeId;
        TradeState = tradeState;
        EntityId = new(fundOrderTradeId.FundId);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack (parameters must align with keys 0–7 in order).
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">The fund entity identifier (key 3).</param>
    /// <param name="errorCode">The error code associated with the command (key 4).</param>
    /// <param name="routeTo">The bounded context to route the command to (key 5).</param>
    /// <param name="fundOrderTradeId">The fund order trade identifier (key 6).</param>
    /// <param name="tradeState">The new trade state (key 7).</param>
    [SerializationConstructor]
    public ChangeFundOrderTradeStateCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FundOrderTradeId fundOrderTradeId,
        TradeState tradeState)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundOrderTradeId = fundOrderTradeId;
        TradeState = tradeState;
    }
}