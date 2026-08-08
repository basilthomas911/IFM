using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the futures trend direction from an RSI signal.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesTrendDirectionFromRSISignalParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TimeFrameType TimePeriod { get; init; }
    [Key(3)] public int PeriodLength { get; init; }
    [Key(4)] public DateTime Timestamp { get; init; }
    [Key(5)] public int LookBackInterval { get; init; }
    [Key(6)] public DateTime StartTime { get; init; }
    [Key(7)] public DateTime EndTime { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesTrendDirectionFromRSISignalParameter() { }

    [SerializationConstructor]
    public GetFuturesTrendDirectionFromRSISignalParameter(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        DateTime timestamp, int lookBackInterval, DateTime startTime, DateTime endTime)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
        LookBackInterval = lookBackInterval;
        StartTime = startTime;
        EndTime = endTime;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&timePeriod={TimePeriod}&periodLength={PeriodLength}&timestamp={Timestamp:yyyy-MM-ddTHH:mm:ss}&lookBackInterval={LookBackInterval}&startTime={StartTime:yyyy-MM-ddTHH:mm:ss}&endTime={EndTime:yyyy-MM-ddTHH:mm:ss}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{TimePeriod}.{PeriodLength}.{Timestamp:yyyy-MM-ddTHH:mm:ss}";
}
