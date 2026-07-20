using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the futures trend direction from an RSI signal.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesTrendDirectionFromRSISignalParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public DateTime Timestamp { get; init; }
    [Key(3)] public int LookBackInterval { get; init; }
    [Key(4)] public DateTime StartTime { get; init; }
    [Key(5)] public DateTime EndTime { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesTrendDirectionFromRSISignalParameter() { }

    [SerializationConstructor]
    public GetFuturesTrendDirectionFromRSISignalParameter(string contractId, DateOnly valueDate, DateTime timestamp, int lookBackInterval, DateTime startTime, DateTime endTime)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        Timestamp = timestamp;
        LookBackInterval = lookBackInterval;
        StartTime = startTime;
        EndTime = endTime;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&timestamp={Timestamp:yyyy-MM-ddTHH:mm:ss}&lookBackInterval={LookBackInterval}&startTime={StartTime:yyyy-MM-ddTHH:mm:ss}&endTime={EndTime:yyyy-MM-ddTHH:mm:ss}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{Timestamp:yyyy-MM-ddTHH:mm:ss}";
}
