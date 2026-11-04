using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Captures the full command state at a committed stream boundary.</summary>
[MessagePackObject]
public sealed record ParameterSetStateSnapshottedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public Guid OwnerId { get; init; }
    [Key(8)] public IReadOnlyDictionary<Guid, string>? Operations { get; init; }
    [Key(9)] public IReadOnlyDictionary<Guid, ParameterAuditEntry>? Audit { get; init; }
    [Key(10)] public long CatalogRevision { get; init; }
    [Key(11)] public IReadOnlyDictionary<int, ParameterSetVersion>? Versions { get; init; }
    [Key(12)] public IReadOnlyDictionary<Guid, ParameterOperationReceipt>? Receipts { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(ParameterSetStateSnapshottedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
