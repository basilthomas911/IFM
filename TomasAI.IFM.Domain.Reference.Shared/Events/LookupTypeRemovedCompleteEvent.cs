using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Shared.Events;

/// <summary>
/// Event published when a lookup type has been removed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeRemovedCompleteEvent : ICompleteEvent<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeEvent";
    [IgnoreMember] public const string Verb = "RemovedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LookupTypeId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public LookupTypeId LookupTypeId { get; init; }
    [Key(9)] public DateTime RemovedOn { get; init; }
    [Key(10)] public string RemovedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public LookupTypeRemovedCompleteEvent() { }

    [SerializationConstructor]
    public LookupTypeRemovedCompleteEvent(
        ActorSubject subject,
        LookupTypeId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LookupTypeId lookupTypeId,
        DateTime removedOn,
        string removedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        LookupTypeId = lookupTypeId ?? throw new ArgumentNullException(nameof(lookupTypeId));
        RemovedOn = removedOn;
        RemovedBy = removedBy ?? string.Empty;
    }
}
