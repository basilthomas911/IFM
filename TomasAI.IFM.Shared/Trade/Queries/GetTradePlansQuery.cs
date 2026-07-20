using MessagePack;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradePlansQuery : IQuery<TradePlanReadModel[]>
{
    [IgnoreMember] public const string Actor = "TradePlansQuery";
    [IgnoreMember] public const string Verb = "GetTradePlans";
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

    public GetTradePlansQuery() { }

    public GetTradePlansQuery(int orderId, int tradeId, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        EntityId = new GetTradePlansParameter(orderId, tradeId, valueDate);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePlansQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        int orderId,
        int tradeId,
        DateOnly valueDate)
    {
        Subject = subject;
        EntityId = new GetTradePlansParameter(orderId, tradeId, valueDate);
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
