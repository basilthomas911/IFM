using MessagePack;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the forward delta data for an Iron Condor strategy based on the specified VIX
/// contract, value date, trade type, and risk position type.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetIronCondorForwardDeltaQuery : IQuery<IronCondorForwardDeltaDataModel>
{
    [IgnoreMember] public const string Actor = "TradePlanQuery";
    [IgnoreMember] public const string Verb = "GetIronCondorForwardDelta";
    [IgnoreMember] public const int ErrorId = 1030;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string VixContractId { get; init; }

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    [Key(4)]
    public TradeType TradeType { get; init; }

    [Key(5)]
    public RiskPositionType RiskPositionType { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetIronCondorForwardDeltaQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetIronCondorForwardDeltaQuery(string vixContractId, DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
    {
        VixContractId = vixContractId ?? string.Empty;
        ValueDate = valueDate;
        TradeType = tradeType;
        RiskPositionType = riskPositionType;
        EntityId = new GetIronCondorForwardDeltaParameter(vixContractId!, valueDate, tradeType, riskPositionType);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetIronCondorForwardDeltaQuery(
        ActorSubject subject,               // Key(0)
        IActorEntityId entityId,            // Key(1)
        string vixContractId,               // Key(2)
        DateOnly valueDate,                 // Key(3)
        TradeType tradeType,                // Key(4)
        RiskPositionType riskPositionType)  // Key(5)
    {
        Subject = subject;
        EntityId = new GetIronCondorForwardDeltaParameter(vixContractId, valueDate, tradeType, riskPositionType);
        VixContractId = vixContractId ?? string.Empty;
        ValueDate = valueDate;
        TradeType = tradeType;
        RiskPositionType = riskPositionType;
        ErrorCode = ErrorId;
    }
}
