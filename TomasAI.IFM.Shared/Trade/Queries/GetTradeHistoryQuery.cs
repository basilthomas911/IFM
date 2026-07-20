using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradeHistoryQuery : IQuery<TradeHistoryReadModel[]>
{
    [IgnoreMember] public const string Actor = "TradeQuery";
    [IgnoreMember] public const string Verb = "GetTradeHistory";
    [IgnoreMember] public const int ErrorId = 1021;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    public GetTradeHistoryQuery() { }

    public GetTradeHistoryQuery(int orderId)
    {
        OrderId = orderId;
        EntityId = new GetTradeHistoryParameter(orderId);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradeHistoryQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId)                // Key(2)
    {
        Subject = subject;
        EntityId = new GetTradeHistoryParameter(orderId);
        OrderId = orderId;
        ErrorCode = ErrorId;
    }
}

