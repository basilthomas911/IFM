using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradeLimitQuery : IQuery<TradeLimitReadModel>
{
    [IgnoreMember] public const string Actor = "TradeQuery";
    [IgnoreMember] public const string Verb = "GetTradeLimit";
    [IgnoreMember] public const int ErrorId = 1022;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int TradeId { get; init; }

    public GetTradeLimitQuery() { }

    public GetTradeLimitQuery(int tradeId)
    {
        TradeId = tradeId;
        EntityId = new GetTradeLimitParameter(tradeId);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradeLimitQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int tradeId)                // Key(2)
    {
        Subject = subject;
        EntityId = new GetTradeLimitParameter(tradeId);
        TradeId = tradeId;
        ErrorCode = ErrorId;
    }
}
