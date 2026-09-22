using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

public sealed record PortfolioSnapshotCaptured(Guid Id, Guid CommandId, long SourceRevision, PortfolioAggregateSnapshot State, DateTime OccurredOnUtc, string Principal)
    : IEvent<ActorEntityId>
{
    public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    public long EventId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = nameof(PortfolioEventStore);
    public DateTime ReceivedOn { get; init; } = OccurredOnUtc;
    public string UserName => Principal;
    public string EventName => nameof(PortfolioSnapshotCaptured);
    public EventType EventType => EventType.DomainEvent;
    public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
}

public sealed record PortfolioFundSnapshotCaptured(Guid Id, Guid CommandId, long SourceRevision, PortfolioFundAggregateSnapshot State, DateTime OccurredOnUtc, string Principal)
    : IEvent<ActorEntityId>
{
    public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    public long EventId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = nameof(PortfolioEventStore);
    public DateTime ReceivedOn { get; init; } = OccurredOnUtc;
    public string UserName => Principal;
    public string EventName => nameof(PortfolioFundSnapshotCaptured);
    public EventType EventType => EventType.DomainEvent;
    public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
}

public sealed record PortfolioEventMetadata(Guid CorrelationId, Guid CausationId, DateTime OriginatedOnUtc)
{
    public static PortfolioEventMetadata ForCommand(Guid commandId, Guid eventId, DateTime occurredOnUtc) =>
        new(commandId, eventId, occurredOnUtc);

    public void Validate()
    {
        if (CorrelationId == Guid.Empty) throw new ArgumentException("CorrelationId is required.", nameof(CorrelationId));
        if (CausationId == Guid.Empty) throw new ArgumentException("CausationId is required.", nameof(CausationId));
        if (OriginatedOnUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("OriginatedOnUtc must be UTC.", nameof(OriginatedOnUtc));
    }
}
