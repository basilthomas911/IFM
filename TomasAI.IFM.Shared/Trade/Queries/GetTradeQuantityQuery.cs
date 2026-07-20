using MessagePack;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradeQuantityQuery : IQuery<ScalarReadModel<int>>
{
    [IgnoreMember] public const string Actor = "TradeQuery";
    [IgnoreMember] public const string Verb = "GetTradeQuantity";
    [IgnoreMember] public const int ErrorId = 1018;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int TradeId { get; init; }

    public GetTradeQuantityQuery() { }

    public GetTradeQuantityQuery(int tradeId)
    {
        TradeId = tradeId;
        EntityId = new GetTradeQuantityParameter(tradeId);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradeQuantityQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        int tradeId)
    {
        Subject = subject;
        EntityId = new GetTradeQuantityParameter(tradeId);
        TradeId = tradeId;
        ErrorCode = ErrorId;
    }
}
