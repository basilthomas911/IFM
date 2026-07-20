using MessagePack;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradeTypeLimitQuery : IQuery<TradeTypeLimitReadModel>
{
    [IgnoreMember] public const string Actor = "TradeQuery";
    [IgnoreMember] public const string Verb = "GetTradeTypeLimit";
    [IgnoreMember] public const int ErrorId = 1022;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int TradeId { get; init; }

    [Key(3)]
    public TradeType TradeType { get; init; }

    public GetTradeTypeLimitQuery() { }

    public GetTradeTypeLimitQuery(int tradeId, TradeType tradeType)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        EntityId = new GetTradeTypeLimitParameter(tradeId, tradeType);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradeTypeLimitQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        int tradeId,
        TradeType tradeType)
    {
        Subject = subject;
        EntityId = new GetTradeTypeLimitParameter(tradeId, tradeType);
        TradeId = tradeId;
        TradeType = tradeType;
        ErrorCode = ErrorId;
    }
}
