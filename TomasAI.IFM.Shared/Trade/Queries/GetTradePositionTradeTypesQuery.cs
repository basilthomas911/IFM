using MessagePack;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradePositionTradeTypesQuery : IQuery<string[]>
{
    [IgnoreMember] public const string Actor = "TradePositionTradeTypesQuery";
    [IgnoreMember] public const string Verb = "GetTradePositionTradeTypes";
    [IgnoreMember] public const int ErrorId = 1021;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    [Key(3)]
    public int TradeId { get; init; }

    [Key(4)]
    public DateOnly ValueDate { get; init; }

    [Key(5)]
    public int DaysToExpiry { get; init; }

    [Key(6)]
    public TradeStatus TradeStatus { get; init; }

    public GetTradePositionTradeTypesQuery() { }

    public GetTradePositionTradeTypesQuery(int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        EntityId = new GetTradePositionTradeTypesParameter(orderId, tradeId, valueDate, daysToExpiry, tradeStatus);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePositionTradeTypesQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus)
    {
        Subject = subject;
        EntityId = new GetTradePositionTradeTypesParameter(orderId, tradeId, valueDate, daysToExpiry, tradeStatus);
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        ErrorCode = ErrorId;
    }
}
