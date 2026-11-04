using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Captures the full command state at a committed stream boundary.</summary>
[MessagePackObject]
public sealed record ParameterStartupStateSnapshottedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public Guid RunId { get; init; }
    [Key(8)] public long Revision { get; init; }
    [Key(9)] public bool Released { get; init; }
    [Key(10)] public ParameterStartupRun? ActiveRun { get; init; }
    [Key(11)] public ParameterSignalStartupReport? Report { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(ParameterStartupStateSnapshottedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
