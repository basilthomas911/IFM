using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradePlacementStoppedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradePlacementEvent";
        [IgnoreMember] public const string Verb = "Stopped";
        [IgnoreMember] public const int ErrorCode = 11063;
        [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public string EntityId { get; init; }
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public DateTime ReceivedOn { get; init; }

        // payload (keys 8..)
        [Key(8)] public TradePlacementId TradePlacementId { get; init; }
        [Key(9)] public DateTime StoppedOn { get; init; }
        [Key(10)] public string StoppedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradePlacementStoppedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradePlacementStoppedEvent() { }

        [SerializationConstructor]
        public TradePlacementStoppedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradePlacementId tradePlacementId,
            DateTime stoppedOn,
            string stoppedBy)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            TradePlacementId = tradePlacementId;
            StoppedOn = stoppedOn;
            StoppedBy = stoppedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => throw new NotImplementedException();
        public IErrorEvent ToFailedEvent(Exception ex) => throw new NotImplementedException();
    }


}
