using MessagePack;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// Represents a query to retrieve trade positions associated with a specific order and trade.
/// </summary>
/// <remarks>This query is used to fetch an array of trade positions based on the provided order and trade
/// identifiers. The query parameters are constructed using the <see cref="OrderId"/> and <see cref="TradeId"/>
/// properties.</remarks>
/// <param name="orderId">The unique identifier of the order. Must be a valid, non-negative integer.</param>
/// <param name="tradeId">The unique identifier of the trade. Must be a valid, non-negative integer.</param>
[MessagePackObject(AllowPrivate = true)]
public record GetTradePositionsQuery : IQuery<TradePositionReadModel[]>
{
    [IgnoreMember] public const string Actor = "TradePositionsQuery";
    [IgnoreMember] public const string Verb = "GetTradePositions";
    [IgnoreMember] public const int ErrorId = 1018;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    [Key(3)]
    public int TradeId { get; init; }

    public GetTradePositionsQuery() { }

    public GetTradePositionsQuery(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
        EntityId = new GetTradePositionsParameter(orderId, tradeId);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePositionsQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        int orderId,
        int tradeId)
    {
        Subject = subject;
        EntityId = new GetTradePositionsParameter(orderId, tradeId);
        OrderId = orderId;
        TradeId = tradeId;
        ErrorCode = ErrorId;
    }
}

