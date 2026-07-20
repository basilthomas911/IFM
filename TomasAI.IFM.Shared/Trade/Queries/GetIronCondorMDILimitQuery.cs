using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the Iron Condor Maximum Drawdown Impact (MDI) limit data for a specific order and
/// trade on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetIronCondorMDILimitQuery : IQuery<IronCondorMDILimitDataModel>
{
    [IgnoreMember] public const string Actor = "IronCondorMDILimitQuery";
    [IgnoreMember] public const string Verb = "GetIronCondorMDILimit";
    [IgnoreMember] public const int ErrorId = 1023;

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

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetIronCondorMDILimitQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetIronCondorMDILimitQuery(int orderId, int tradeId, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        EntityId = new GetIronCondorMDILimitParameter(orderId, tradeId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetIronCondorMDILimitQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int orderId,                // Key(2)
        int tradeId,                // Key(3)
        DateOnly valueDate)         // Key(4)
    {
        Subject = subject;
        EntityId = new GetIronCondorMDILimitParameter(orderId, tradeId, valueDate);
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}

