using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to turn off the live feed for a specific trade within an order.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern (base command keys 0–5). Custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.MarketDataFeedBoundedContext"/> and uses error code 4021.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TurnTradeLiveFeedOffCommand
    : ICommand<TradeLiveFeedId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "TurnTradeLiveFeedOff";
    public const int ErrorId = 4021;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradeLiveFeedId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The unique identifier of the order associated with the trade.</summary>
    [Key(6)]
    public int OrderId { get; init; }

    /// <summary>The unique identifier of the trade for which the live feed is to be turned off.</summary>
    [Key(7)]
    public int TradeId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public TurnTradeLiveFeedOffCommand() { }

    /// <summary>
    /// Creates a new command to turn off the live feed for the specified order/trade.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    public TurnTradeLiveFeedOffCommand(int orderId, int tradeId, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;

        EntityId = new TradeLiveFeedId(OrderId, TradeId, valueDate);
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
        ErrorCode = ErrorId;
    }

    // Explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public TurnTradeLiveFeedOffCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        TradeLiveFeedId entityId,       // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        int orderId,                    // Key(6)
        int tradeId)                    // Key(7)
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