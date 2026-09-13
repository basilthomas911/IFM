using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;

/// <summary>
/// Command to add (start) a live market data feed for a specific trade belonging to an order.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. The command is routed to
/// <see cref="BoundedContextName.MarketDataFeedBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record HaltTradeLiveFeedCommand : ICommand<TradeEntityId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "HaltTradeLiveFeed";
    public const int ErrorId = 4021;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradeEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [IgnoreMember] public int PortfolioId => EntityId.PortfolioId;
    [IgnoreMember] public int FundId => EntityId.FundId;
    [IgnoreMember] public int OrderId => EntityId.OrderId;
    [IgnoreMember] public int TradeId => EntityId.TradeId;

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public HaltTradeLiveFeedCommand() { }

    /// <summary>
    /// Creates a new command to add a live feed for the specified trade.
    /// </summary>
    /// <param name="entityId">Globally unique Portfolio, Fund, Order, and Trade identity.</param>
    public HaltTradeLiveFeedCommand(TradeEntityId entityId)
    {
        EntityId = entityId;
        ErrorCode = 4021;
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public HaltTradeLiveFeedCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        TradeEntityId entityId,         // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo)     // Key(5)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
    }
}
