using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;

/// <summary>Command to deactivate the live feed for one globally identified trade.</summary>
[MessagePackObject(AllowPrivate = true)]
public record TurnTradeLiveFeedOffCommand : ICommand<TradeEntityId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "TurnTradeLiveFeedOff";
    public const int ErrorId = 4021;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradeEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public DateOnly ValueDate { get; init; }

    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public int PortfolioId => EntityId.PortfolioId;
    [IgnoreMember] public int FundId => EntityId.FundId;
    [IgnoreMember] public int OrderId => EntityId.OrderId;
    [IgnoreMember] public int TradeId => EntityId.TradeId;

    public TurnTradeLiveFeedOffCommand() { }

    public TurnTradeLiveFeedOffCommand(TradeEntityId entityId, DateOnly valueDate)
    {
        EntityId = entityId;
        ValueDate = valueDate;
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public TurnTradeLiveFeedOffCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        TradeEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        DateOnly valueDate)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ValueDate = valueDate;
    }
}
