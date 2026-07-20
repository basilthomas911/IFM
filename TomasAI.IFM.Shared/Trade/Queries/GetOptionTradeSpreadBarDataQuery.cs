using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve spread bar data for an option trade within a specified date range.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetOptionTradeSpreadBarDataQuery : IQuery<OptionTradeSpreadBarsDataModel[]>
{
    [IgnoreMember] public const string Actor = "OptionTradeSpreadBarDataQuery";
    [IgnoreMember] public const string Verb = "GetOptionTradeSpreadBarData";
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

    [Key(6)]
    public DateTime StartDate { get; init; }

    [Key(7)]
    public DateTime EndDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetOptionTradeSpreadBarDataQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetOptionTradeSpreadBarDataQuery(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        EntityId = new GetOptionTradeSpreadBarDataParameter(orderId, tradeId, tradeType, valueDate, startDate, endDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetOptionTradeSpreadBarDataQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId,                // Key(3)
        TradeType tradeType,        // Key(4)
        DateOnly valueDate,         // Key(5)
        DateTime startDate,         // Key(6)
        DateTime endDate)           // Key(7)
    {
        Subject = subject;
        EntityId = new GetOptionTradeSpreadBarDataParameter(orderId, tradeId, tradeType, valueDate, startDate, endDate);
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        ErrorCode = ErrorId;
    }
}
