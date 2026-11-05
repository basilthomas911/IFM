using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

/// <summary>Full policy state captured at a committed business stream revision.</summary>
public sealed record PortfolioFinancialPolicySnapshotCaptured(Guid Id, Guid CommandId, long SourceRevision,
    PortfolioFinancialPolicyAggregateSnapshot State, DateTime OccurredOnUtc, string Principal) : IEvent<ActorEntityId>
{
    public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    public long EventId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = nameof(PortfolioEventStore);
    public DateTime ReceivedOn { get; init; } = OccurredOnUtc;
    public string UserName => Principal;
    public string EventName => nameof(PortfolioFinancialPolicySnapshotCaptured);
    public EventType EventType => EventType.DomainEvent;
    public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
}
