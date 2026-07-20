using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record FuturesItiTrendModelBuildStartedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "FuturesItiTrendEvent";
        [IgnoreMember] public const string Verb = "ModelBuildStarted";
        [IgnoreMember] public const int ErrorCode = 19002;
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
        [Key(10)] public DateTime StartedOn { get; init; }
        [Key(11)] public string StartedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(FuturesItiTrendModelBuildStartedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public FuturesItiTrendModelBuildStartedEvent() { }

        [SerializationConstructor]
        public FuturesItiTrendModelBuildStartedEvent(
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
            DateTime startedOn,
            string startedBy)
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
            StartedOn = startedOn;
            StartedBy = startedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => default;
        public IErrorEvent ToFailedEvent(Exception ex) => default;
    }


}
