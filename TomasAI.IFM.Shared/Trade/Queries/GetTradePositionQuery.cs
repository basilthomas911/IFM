using MessagePack;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradePositionQuery : IQuery<TradePositionReadModel>
{
    [IgnoreMember] public const string Actor = "TradeQuery";
    [IgnoreMember] public const string Verb = "GetTradePosition";
    [IgnoreMember] public const int ErrorId = 1020;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    [Key(3)]
    public int TradeId { get; init; }

    [Key(4)]
    public TradeType TradeType { get; init; }

    [Key(5)]
    public DateOnly ValueDate { get; init; }

    [Key(6)]
    public int DaysToExpiry { get; init; }

    [Key(7)]
    public TradeStatus TradeStatus { get; init; }

    public GetTradePositionQuery() { }

    public GetTradePositionQuery(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        EntityId = new GetTradePositionParameter(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePositionQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus)
    {
        Subject = subject;
        EntityId = new GetTradePositionParameter(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus);
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        ErrorCode = ErrorId;
    }
}
