using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to add (start) a live market data feed for a specific trade belonging to an order.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. The command is routed to
/// <see cref="BoundedContextName.MarketDataFeedBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddTradeLiveFeedCommand : ICommand<TradeOrderId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "AddTradeLiveFeed";
    public const int ErrorId = 4021;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradeOrderId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Unique identifier of the order containing the trade.
    /// </summary>
    [Key(6)]
    public int OrderId { get; init; }

    /// <summary>
    /// Unique identifier of the trade for which the live feed is being added.
    /// </summary>
    [Key(7)]
    public int TradeId { get; init; }

    [Key(8)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public AddTradeLiveFeedCommand() { }

    /// <summary>
    /// Creates a new command to add a live feed for the specified trade.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="valueDate">Value date.</param>
    public AddTradeLiveFeedCommand(int orderId, int tradeId, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;

        EntityId = new TradeOrderId(OrderId, TradeId);
        ErrorCode = 4021;
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public AddTradeLiveFeedCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        TradeOrderId entityId,          // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        int orderId,                    // Key(6)
        int tradeId,                    // Key(7)
        DateOnly valueDate)             // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
    }
}