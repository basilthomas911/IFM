using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Events;

/// <summary>Captures the full command state at a committed stream boundary.</summary>
[MessagePackObject]
public sealed record LookupTypeStateSnapshottedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public IReadOnlyDictionary<LookupTypeId, LookupTypeReadModel>? Entries { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(LookupTypeStateSnapshottedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
