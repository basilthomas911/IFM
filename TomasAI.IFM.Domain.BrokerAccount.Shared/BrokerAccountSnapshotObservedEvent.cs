using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Transports one immutable framework account snapshot through the BrokerAccount event mailbox.</summary>
[MessagePackObject]
public sealed record BrokerAccountSnapshotObservedEvent : IEvent<BrokerAccountId>
{
    public const string Actor = BrokerAccountActorNames.Event;
    public const string Verb = "BrokerAccountSnapshotObserved";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public BrokerAccountId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public BrokerEnvironment Environment { get; init; }
    [Key(9)] public BrokerAccountSnapshotEvidence Snapshot { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(BrokerAccountSnapshotObservedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
