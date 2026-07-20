using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to activate the live feed for a specific trade within an order.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.MarketDataFeedBoundedContext"/> (error code 4021).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TurnTradeLiveFeedOnCommand : ICommand<TradeLiveFeedId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "TurnTradeLiveFeedOn";
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

    /// <summary>The order identifier.</summary>
    [Key(6)]
    public int OrderId { get; init; }

    /// <summary>The trade identifier.</summary>
    [Key(7)]
    public int TradeId { get; init; }

    /// <summary>Parameterless constructor for MessagePack.</summary>
    public TurnTradeLiveFeedOnCommand() { }

    /// <summary>Create command to turn on live feed.</summary>
    public TurnTradeLiveFeedOnCommand(int orderId, int tradeId, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        EntityId = new TradeLiveFeedId(OrderId, TradeId, valueDate);
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
        ErrorCode = 4021;
    }

    [SerializationConstructor]
    public TurnTradeLiveFeedOnCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        TradeLiveFeedId entityId,
        int errorCode,
        BoundedContextName routeTo,
        int orderId,
        int tradeId)
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