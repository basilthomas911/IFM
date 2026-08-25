using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;

/// <summary>Identifies a complete Futures ITI history request for one display timeframe.</summary>
[MessagePackObject(false)]
public record GetFuturesItiSignalHistoryParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TimeFrameType TimePeriod { get; init; }
    [IgnoreMember] public string? QueryParams { get; private set; }

    public GetFuturesItiSignalHistoryParameter() { }

    [SerializationConstructor]
    public GetFuturesItiSignalHistoryParameter(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&timePeriod={TimePeriod}";
    }

    public string Format() => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{TimePeriod}";
}
