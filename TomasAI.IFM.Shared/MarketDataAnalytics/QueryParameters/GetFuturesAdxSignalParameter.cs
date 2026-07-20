using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a futures ADX signal for a specific contract and value date.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesAdxSignalParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }
    [Key(3)] public int PeriodLength { get; init; } 

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesAdxSignalParameter() { }

    [SerializationConstructor]
    public GetFuturesAdxSignalParameter(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&timePeriod={timePeriod}&periodLength={periodLength}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{TimePeriod}.{PeriodLength}";
}
