using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradePlanActionQuery : IQuery<TradePlanActionReadModel[]>
{
    [IgnoreMember] public const string Actor = "TradePlanActionQuery";
    [IgnoreMember] public const string Verb = "GetTradePlanAction";
    [IgnoreMember] public const int ErrorId = 1028;

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

    public GetTradePlanActionQuery() { }

    public GetTradePlanActionQuery(int orderId, int tradeId, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        EntityId = new GetTradePlanActionParameter(orderId, tradeId, valueDate);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePlanActionQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId,                // Key(3)
        DateOnly valueDate)         // Key(4)
    {
        Subject = subject;
        EntityId = new GetTradePlanActionParameter(orderId, tradeId, valueDate);
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
