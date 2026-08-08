using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the trend direction of a futures contract based on RSI signals.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesTrendDirectionFromRSISignalQuery : IQuery<FuturesTrendDirectionReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesTrendDirectionFromRSISignal";
    [IgnoreMember] public const int ErrorId = 1011;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; }

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    [Key(4)]
    public TimeFrameType TimePeriod { get; init; }

    [Key(5)]
    public int PeriodLength { get; init; }

    [Key(6)]
    public DateTime Timestamp { get; init; }

    [Key(7)]
    public int LookBackInterval { get; init; }

    [Key(8)]
    public DateTime StartTime { get; init; }

    [Key(9)]
    public DateTime EndTime { get; init; }

    public GetFuturesTrendDirectionFromRSISignalQuery() { }

    public GetFuturesTrendDirectionFromRSISignalQuery(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        DateTime timestamp,
        int lookBackInterval,
        DateTime startTime,
        DateTime endTime)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
        LookBackInterval = lookBackInterval;
        StartTime = startTime;
        EndTime = endTime;
        EntityId = new GetFuturesTrendDirectionFromRSISignalParameter(
            contractId, valueDate, timePeriod, periodLength, timestamp, lookBackInterval, startTime, endTime);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesTrendDirectionFromRSISignalQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId,        // Key(2)
        DateOnly valueDate,       // Key(3)
        TimeFrameType timePeriod, // Key(4)
        int periodLength,         // Key(5)
        DateTime timestamp,       // Key(6)
        int lookBackInterval,     // Key(7)
        DateTime startTime,       // Key(8)
        DateTime endTime)         // Key(9)
    {
        Subject = subject;
        EntityId = new GetFuturesTrendDirectionFromRSISignalParameter(
            contractId, valueDate, timePeriod, periodLength, timestamp, lookBackInterval, startTime, endTime);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
        LookBackInterval = lookBackInterval;
        StartTime = startTime;
        EndTime = endTime;
        ErrorCode = ErrorId;
    }
}


