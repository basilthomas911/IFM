using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record ScheduledJobRemovedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "ScheduledJobEvent";
        [IgnoreMember] public const string Verb = "Removed";
        [IgnoreMember] public const int ErrorCode = 0;
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
        [Key(8)] public string ScheduledJobName { get; init; }
        [Key(9)] public DateTime RemovedOn { get; init; }
        [Key(10)] public string RemovedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(ScheduledJobRemovedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public ScheduledJobRemovedEvent() { }

        [SerializationConstructor]
        public ScheduledJobRemovedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            string scheduledJobName,
            DateTime removedOn,
            string removedBy)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            ScheduledJobName = scheduledJobName ?? string.Empty;
            RemovedOn = removedOn;
            RemovedBy = removedBy ?? string.Empty;
        }
    }

}
