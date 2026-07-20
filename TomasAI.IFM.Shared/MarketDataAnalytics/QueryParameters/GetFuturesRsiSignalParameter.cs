using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a futures RSI signal for a specific contract, value date, and signal type.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesRsiSignalParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; } 
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }
    [Key(3)] public int PeriodLength { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesRsiSignalParameter() { }

    [SerializationConstructor]
    public GetFuturesRsiSignalParameter(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate}&timePeriod={TimePeriod}&periodLength={PeriodLength}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate}.{TimePeriod}.{PeriodLength}";
}
