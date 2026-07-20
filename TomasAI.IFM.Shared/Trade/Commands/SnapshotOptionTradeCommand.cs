using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to snapshot an option trade identified by OrderId and TradeId.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4009.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record SnapshotOptionTradeCommand
    : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "Snapshot";
    public const int ErrorId = 4009;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public OptionTradeEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The parent order identifier.</summary>
    [Key(6)]
    public int OrderId { get; init; }

    /// <summary>The trade identifier within the order.</summary>
    [Key(7)]
    public int TradeId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public SnapshotOptionTradeCommand() { }

    /// <summary>
    /// Creates a new command to snapshot the specified option trade.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    public SnapshotOptionTradeCommand(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;

        EntityId = new OptionTradeEntityId(OrderId, TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public SnapshotOptionTradeCommand(
        Guid commandId,                // Key(0)
        ActorSubject subject,          // Key(1)
        bool postEvents,               // Key(2)
        OptionTradeEntityId entityId,  // Key(3)
        int errorCode,                 // Key(4)
        BoundedContextName routeTo,    // Key(5)
        int orderId,                   // Key(6)
        int tradeId)                   // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        OrderId = orderId;
        TradeId = tradeId;
    }
}