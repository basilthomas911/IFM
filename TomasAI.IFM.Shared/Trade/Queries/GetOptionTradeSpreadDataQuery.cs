using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve option trade spread data.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetOptionTradeSpreadDataQuery : IQuery<OptionTradeSpreadsDataModel>
{
    [IgnoreMember] public const string Actor = "OptionTradeSpreadDataQuery";
    [IgnoreMember] public const string Verb = "GetOptionTradeSpreadData";
    [IgnoreMember] public const int ErrorId = 1019;

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

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetOptionTradeSpreadDataQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetOptionTradeSpreadDataQuery(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        EntityId = new GetOptionTradeSpreadDataParameter(orderId, tradeId, tradeType, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetOptionTradeSpreadDataQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId,                // Key(3)
        TradeType tradeType,        // Key(4)
        DateOnly valueDate)         // Key(5)
    {
        Subject = subject;
        EntityId = new GetOptionTradeSpreadDataParameter(orderId, tradeId, tradeType, valueDate);
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
