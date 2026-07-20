using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the iron condor forward delta data.
/// </summary>
[MessagePackObject(false)]
public record GetIronCondorForwardDeltaParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string VixContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TradeType TradeType { get; init; }
    [Key(3)] public RiskPositionType RiskPositionType { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetIronCondorForwardDeltaParameter() { }

    [SerializationConstructor]
    public GetIronCondorForwardDeltaParameter(string vixContractId, DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
    {
        VixContractId = vixContractId ?? string.Empty;
        ValueDate = valueDate;
        TradeType = tradeType;
        RiskPositionType = riskPositionType;
        QueryParams = $"vixContractId={VixContractId}&valueDate={ValueDate:yyyy-MM-dd}&tradeType={TradeType}&riskPositionType={RiskPositionType}";
    }

    public string Format()
        => $"{VixContractId}.{ValueDate:yyyyMMdd}.{TradeType}.{RiskPositionType}";
}
