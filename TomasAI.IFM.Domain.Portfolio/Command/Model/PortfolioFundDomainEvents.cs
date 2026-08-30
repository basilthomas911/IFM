using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Command.Model;

public abstract record PortfolioFundDomainEvent(Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal)
    : IEvent<ActorEntityId>
{
    public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    public long EventId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = "PortfolioFund";
    public DateTime ReceivedOn { get; init; } = OccurredOnUtc;
    public string UserName => Principal;
    public string EventName => GetType().Name;
    public EventType EventType => EventType.DomainEvent;
    public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public DateTime OriginatedOnUtc { get; init; } = OccurredOnUtc;
}

public sealed record FundMandateCreated(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundMandateReadModel Mandate)
    : PortfolioFundDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal)
{
    public Guid IdempotencyKey { get; init; }
}

public sealed record FundMandateVersionAdded(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundMandateReadModel Mandate)
    : PortfolioFundDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record FundOperatingStateChanged(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundOperatingState State, string Reason)
    : PortfolioFundDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record FundTradeTemplateAssigned(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundTradeTemplateAssignmentReadModel Assignment)
    : PortfolioFundDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record FundCompositionReserved(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundCompositionReservationResult Reservation)
    : PortfolioFundDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record FundCompositionStateChanged(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    FundOrderProjectionReadModel Order)
    : PortfolioFundDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);
