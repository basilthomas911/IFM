using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Command.Model;

public abstract record PortfolioDomainEvent(Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal)
    : IEvent<ActorEntityId>
{
    public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    public long EventId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = "Portfolio";
    public DateTime ReceivedOn { get; init; } = OccurredOnUtc;
    public string UserName => Principal;
    public string EventName => GetType().Name;
    public EventType EventType => EventType.DomainEvent;
    public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public DateTime OriginatedOnUtc { get; init; } = OccurredOnUtc;
}

public sealed record PortfolioCreated(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    PortfolioReadModel Portfolio)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal)
{
    public Guid IdempotencyKey { get; init; }
}

public sealed record PortfolioVersionAdded(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    PortfolioReadModel Portfolio)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record PortfolioOperatingStateChanged(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    PortfolioOperatingState State, string Reason)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record FundAddedToPortfolio(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    PortfolioFundId FundId)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record PortfolioRetired(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal, string Reason)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record FundAllocationDelegated(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundAllocationReadModel Allocation)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record FundRiskEnvelopeDelegated(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundRiskEnvelopeReadModel Envelope)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);
