using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;

/// <summary>Identifies a complete Futures ITI history request for one display timeframe.</summary>
[MessagePackObject(false)]
public record GetFuturesItiSignalHistoryParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string Symbol { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TimeFrameType TimePeriod { get; init; }
    [IgnoreMember] public string? QueryParams { get; private set; }

    public GetFuturesItiSignalHistoryParameter() { }

    [SerializationConstructor]
    public GetFuturesItiSignalHistoryParameter(
        string symbol,
        DateOnly valueDate,
        TimeFrameType timePeriod)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        QueryParams = $"symbol={Symbol}&valueDate={ValueDate:yyyy-MM-dd}&timePeriod={TimePeriod}";
    }

    public string Format() => $"{Symbol}.{ValueDate:yyyy-MM-dd}.{TimePeriod}";
}
