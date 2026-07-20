using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record FuturesItiTrendModelLoadedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "FuturesItiTrendEvent";
        [IgnoreMember] public const string Verb = "ModelLoaded";
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
        [Key(8)] public DateTime LoadedOn { get; init; }
        [Key(9)] public string LoadedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(FuturesItiTrendModelLoadedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public FuturesItiTrendModelLoadedEvent() { }

        [SerializationConstructor]
        public FuturesItiTrendModelLoadedEvent(
            ActorSubject subject,
            Guid id,
            FuturesItiTrendEntityId entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
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
            LoadedOn = loadedOn;
            LoadedBy = loadedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new FuturesItiTrendModelLoadedCompleteEvent
        {
            CommandId = CommandId,
            EntityId = EntityId,
            LoadedOn = LoadedOn,
            LoadedBy = LoadedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new FuturesItiTrendModelLoadedFailEvent
        {
            CommandId = CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record FuturesItiTrendModelLoadedCompleteEvent : CompleteEvent
    {
        public FuturesItiTrendEntityId EntityId { get; init; }
        public DateTime LoadedOn { get; init; }
        public string LoadedBy { get; init; }
    }

    public record FuturesItiTrendModelLoadedFailEvent : ErrorEvent
    {
    }
}
