using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradePlanForwardLossLimitQuery : IQuery<TradePlanForwardLossLimitReadModel>
{
    [IgnoreMember] public const string Actor = "TradePlanForwardLossLimitQuery";
    [IgnoreMember] public const string Verb = "GetTradePlanForwardLossLimit";
    [IgnoreMember] public const int ErrorId = 1030;

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

    public GetTradePlanForwardLossLimitQuery() { }

    public GetTradePlanForwardLossLimitQuery(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        EntityId = new GetTradePlanForwardLossLimitParameter(orderId, tradeId, tradeType, valueDate);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePlanForwardLossLimitQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId,                // Key(3)
        TradeType tradeType,        // Key(4)
        DateOnly valueDate)         // Key(5)
    {
        Subject = subject;
        EntityId = new GetTradePlanForwardLossLimitParameter(orderId, tradeId, tradeType, valueDate);
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}

