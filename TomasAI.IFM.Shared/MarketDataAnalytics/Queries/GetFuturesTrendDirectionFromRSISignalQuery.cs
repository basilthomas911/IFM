using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

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
    public DateTime Timestamp { get; init; }

    [Key(5)]
    public int LookBackInterval { get; init; }

    [Key(6)]
    public DateTime StartTime { get; init; }

    [Key(7)]
    public DateTime EndTime { get; init; }

    public GetFuturesTrendDirectionFromRSISignalQuery() { }

    public GetFuturesTrendDirectionFromRSISignalQuery(
        string contractId,
        DateOnly valueDate,
        DateTime timestamp,
        int lookBackInterval,
        DateTime startTime,
        DateTime endTime)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        Timestamp = timestamp;
        LookBackInterval = lookBackInterval;
        StartTime = startTime;
        EndTime = endTime;
        EntityId = new GetFuturesTrendDirectionFromRSISignalParameter(contractId, valueDate, timestamp, lookBackInterval, startTime, endTime);
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
        DateTime timestamp,       // Key(4)
        int lookBackInterval,     // Key(5)
        DateTime startTime,       // Key(6)
        DateTime endTime)         // Key(7)
    {
        Subject = subject;
        EntityId = new GetFuturesTrendDirectionFromRSISignalParameter(contractId, valueDate, timestamp, lookBackInterval, startTime, endTime);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        Timestamp = timestamp;
        LookBackInterval = lookBackInterval;
        StartTime = startTime;
        EndTime = endTime;
        ErrorCode = ErrorId;
    }
}


