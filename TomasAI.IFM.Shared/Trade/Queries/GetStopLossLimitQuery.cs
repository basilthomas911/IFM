using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the stop loss limit for a specific order and trade.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetStopLossLimitQuery : IQuery<TradePlanStopLossLimitReadModel>
{
    [IgnoreMember] public const string Actor = "TradePlanQuery";
    [IgnoreMember] public const string Verb = "GetStopLossLimit";
    [IgnoreMember] public const int ErrorId = 1033;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int OrderId { get; init; }

    [Key(3)]
    public int TradeId { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetStopLossLimitQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetStopLossLimitQuery(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
        EntityId = new GetStopLossLimitParameter(orderId, tradeId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetStopLossLimitQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId)                // Key(3)
    {
        Subject = subject;
        EntityId = new GetStopLossLimitParameter(orderId, tradeId);
        OrderId = orderId;
        TradeId = tradeId;
        ErrorCode = ErrorId;
    }
}

