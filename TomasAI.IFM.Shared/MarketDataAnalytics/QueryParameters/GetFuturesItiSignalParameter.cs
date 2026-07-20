using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the most recent futures ITI signal for a specific contract and value date.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesItiSignalParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesItiSignalParameter() { }

    [SerializationConstructor]
    public GetFuturesItiSignalParameter(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&timePeriod={TimePeriod}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{TimePeriod}";
}
