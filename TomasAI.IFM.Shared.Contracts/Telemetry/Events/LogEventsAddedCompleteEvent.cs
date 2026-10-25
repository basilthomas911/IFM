using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Telemetry.ViewModels;

namespace TomasAI.IFM.Shared.Telemetry.Events;

[MessagePackObject(AllowPrivate = true)]
public record LogEventsAddedCompleteEvent : ICompleteEvent<LogEventsId>
{
    [IgnoreMember] public const string Actor = "LogEvents";
    [IgnoreMember] public const string Verb = "AddedComplete";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LogEventsId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public LogEventsId LogEventsId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    [Key(9)] public LogEventReadModel[] LogEvents { get; init; }
    [Key(10)] public DateTime AddedOn { get; init; }
    [Key(11)] public string AddedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(LogEventsAddedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public LogEventsAddedCompleteEvent() { }

    [SerializationConstructor]
    public LogEventsAddedCompleteEvent(
        ActorSubject subject,
        LogEventsId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LogEventsId logEventsId,
        LogEventReadModel[] logEvents,
        DateTime addedOn,
        string addedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        LogEventsId = logEventsId;
        LogEvents = logEvents;
        AddedOn = addedOn;
        AddedBy = addedBy ?? string.Empty;
    }
}
