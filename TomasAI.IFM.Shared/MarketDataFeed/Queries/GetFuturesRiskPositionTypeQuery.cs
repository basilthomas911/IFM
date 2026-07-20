using MessagePack;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the risk position type for futures based on a specific value date and trade type.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesRiskPositionTypeQuery : IQuery<RiskPositionTypeReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetFuturesRiskPositionType";
    [IgnoreMember] public const int ErrorId = 1014;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public DateOnly ValueDate { get; set; }

    [Key(3)]
    public TradeType TradeType { get; set; }

    public GetFuturesRiskPositionTypeQuery() { }

    public GetFuturesRiskPositionTypeQuery(DateOnly valueDate, TradeType tradeType)
    {
        ValueDate = valueDate;
        TradeType = tradeType;
        EntityId = new GetFuturesRiskPositionTypeParameter(valueDate, tradeType);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesRiskPositionTypeQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        DateOnly valueDate,       // Key(2)
        TradeType tradeType)      // Key(3)
    {
        Subject = subject;
        EntityId = new GetFuturesRiskPositionTypeParameter(valueDate, tradeType);
        ValueDate = valueDate;
        TradeType = tradeType;
        ErrorCode = ErrorId;
    }
}


