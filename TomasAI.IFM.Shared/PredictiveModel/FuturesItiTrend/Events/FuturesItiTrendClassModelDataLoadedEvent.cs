using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesItiTrendClassModelDataLoadedEvent : IEvent
{
    [IgnoreMember] public const string Actor = "FuturesItiTrendClassEvent";
    [IgnoreMember] public const string Verb = "ModelDataLoaded";
    [IgnoreMember] public const int ErrorCode = 19003;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesItiTrendEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public DateOnly StartDate { get; init; }
    [Key(9)] public DateOnly EndDate { get; init; }
    [Key(10)] public FuturesItiTrendModelDataStatistics Statistics { get; init; }
    [Key(11)] public DateTime LoadedOn { get; init; }
    [Key(12)] public string LoadedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesItiTrendClassModelDataLoadedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesItiTrendClassModelDataLoadedEvent() { }

    [SerializationConstructor]
    public FuturesItiTrendClassModelDataLoadedEvent(
        ActorSubject subject,
        Guid id,
        FuturesItiTrendEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateOnly startDate,
        DateOnly endDate,
        FuturesItiTrendModelDataStatistics statistics,
        DateTime loadedOn,
        string loadedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        StartDate = startDate;
        EndDate = endDate;
        Statistics = statistics;
        LoadedOn = loadedOn;
        LoadedBy = loadedBy ?? string.Empty;
    }

    public ICompleteEvent ToCompletedEvent() => new FuturesItiTrendClassModelDataLoadedCompleteEvent
    {
        CommandId = CommandId,
        EntityId = EntityId,
        StartDate = StartDate,
        EndDate = EndDate,
        Statistics = Statistics,
        LoadedOn = LoadedOn,
        LoadedBy = LoadedBy
    };
    public IErrorEvent ToFailedEvent(Exception ex) => new FuturesItiTrendClassModelDataLoadedFailEvent
    {
        CommandId = CommandId,
        ErrorMessage = ex.Message,
        ErrorType = ErrorType.Command,
        ErrorCode = ErrorCode
    };
}


public record FuturesItiTrendClassModelDataLoadedCompleteEvent : CompleteEvent
{
    public new FuturesItiTrendEntityId EntityId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public FuturesItiTrendModelDataStatistics Statistics { get; init; }
    public DateTime LoadedOn { get; init; }
    public string LoadedBy { get; init; }
}

public record FuturesItiTrendClassModelDataLoadedFailEvent : ErrorEvent
{
}
