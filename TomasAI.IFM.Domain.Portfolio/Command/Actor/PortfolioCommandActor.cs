using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioCommandActor(
    ICommandActorContext<PortfolioCommandActor> context,
    IPortfolioEventStore eventStore,
    IEventProjector<PortfolioCommandActor> projector,
    ILogger<PortfolioCommandActor> logger)
    : BaseEventSourceCommandActor<PortfolioCommandActor>(context, logger)
{
    public const string ActorName = PortfolioCommandSubjects.PortfolioActor;
    const string Principal = "portfolio-nats";
    readonly IPortfolioEventStore _events = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    readonly IEventProjector<PortfolioCommandActor> _projector = projector ?? throw new ArgumentNullException(nameof(projector));

    protected override ValueTask OnStartup(ICommandActorContext<PortfolioCommandActor> context, CancellationToken cancellationToken) =>
        _projector.StartAsync(context, cancellationToken);

    protected override ValueTask OnShutdown(ICommandActorContext<PortfolioCommandActor> context) => _projector.StopAsync();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap = new Dictionary<string, Func<IActorMessage, ICommand>>
    {
        ["CreatePortfolio"] = x => x.AsCommand<PortfolioCommand<CreatePortfolioPayload, PortfolioId>>()!,
        ["AddPortfolioVersion"] = x => x.AsCommand<PortfolioCommand<AddPortfolioVersionPayload, PortfolioId>>()!,
        ["ChangePortfolioOperatingState"] = x => x.AsCommand<PortfolioCommand<ChangePortfolioStatePayload, PortfolioId>>()!,
        ["AddFundToPortfolio"] = x => x.AsCommand<PortfolioCommand<AddFundPayload, PortfolioId>>()!,
        ["DelegateFundAllocation"] = x => x.AsCommand<PortfolioCommand<DelegateAllocationPayload, PortfolioId>>()!,
        ["DelegateFundRiskEnvelope"] = x => x.AsCommand<PortfolioCommand<DelegateRiskEnvelopePayload, PortfolioId>>()!,
        ["RetirePortfolio"] = x => x.AsCommand<PortfolioCommand<RetirePortfolioPayload, PortfolioId>>()!,
    };

    protected override ICommand ParseMessage(ICommandActorContext<PortfolioCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, ParseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<PortfolioCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        if (command.CommandId == Guid.Empty || !int.TryParse(command.Subject.EntityId, out var value) || new PortfolioId(value).Validate().Count != 0)
            throw new ArgumentException("A valid Portfolio command identity is required.");
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<PortfolioCommandActor> context, ActorThreadId threadId, ICommand command) =>
        new PortfolioActorState(ParseId(command), await _events.LoadPortfolioAsync(ParseId(command)).ConfigureAwait(false));

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioCommandActor> context, IActorState state, ICommand command) =>
        ReceiveCoreAsync((PortfolioActorState)state, command, CancellationToken.None);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken) =>
        ReceiveCoreAsync((PortfolioActorState)state, command, cancellationToken);

    async ValueTask<ServiceResult<GuidResult>> ReceiveCoreAsync(PortfolioActorState state, ICommand command, CancellationToken cancellationToken)
    {
        var committed = await _events.FindCommittedPortfolioCommandAsync(state.PortfolioId, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (command is PortfolioCommand<CreatePortfolioPayload, PortfolioId> create && committed is PortfolioCreated prior &&
                !string.Equals(PortfolioCanonicalHash.Compute(create.Payload.Portfolio.DefensiveCopy()), PortfolioCanonicalHash.Compute(prior.Portfolio.DefensiveCopy()), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Portfolio payload.");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (command is PortfolioCommand<CreatePortfolioPayload, PortfolioId> requestedCreate)
        {
            var priorCreate = await _events.FindPortfolioCreateByIdempotencyKeyAsync(state.PortfolioId, requestedCreate.Payload.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (priorCreate is not null)
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Portfolio payload.");
        }
        var now = DateTime.UtcNow;
        PortfolioDomainEvent domainEvent = command switch
        {
            PortfolioCommand<CreatePortfolioPayload, PortfolioId> x => ((PortfolioCreated)state.Aggregate.Create(x.CommandId, x.Payload.Portfolio, now, Principal)) with { IdempotencyKey = x.Payload.IdempotencyKey },
            PortfolioCommand<AddPortfolioVersionPayload, PortfolioId> x => state.Aggregate.AddVersion(x.CommandId, x.Payload.ExpectedVersion, x.Payload.Portfolio, now, Principal),
            PortfolioCommand<ChangePortfolioStatePayload, PortfolioId> x => state.Aggregate.ChangeState(x.CommandId, x.Payload.ExpectedVersion, x.Payload.State, x.Payload.Reason, now, Principal),
            PortfolioCommand<AddFundPayload, PortfolioId> x => state.Aggregate.AddFund(x.CommandId, x.Payload.ExpectedPortfolioVersion, x.Payload.FundId, now, Principal),
            PortfolioCommand<DelegateAllocationPayload, PortfolioId> x => state.Aggregate.DelegateAllocation(x.CommandId, x.Payload.ExpectedPortfolioVersion, x.Payload.Allocation, now, Principal),
            PortfolioCommand<DelegateRiskEnvelopePayload, PortfolioId> x => state.Aggregate.DelegateRiskEnvelope(x.CommandId, x.Payload.ExpectedPortfolioVersion, x.Payload.Envelope, now, Principal),
            PortfolioCommand<RetirePortfolioPayload, PortfolioId> x => state.Aggregate.Retire(x.CommandId, x.Payload.ExpectedVersion, x.Payload.Reason, now, Principal),
            _ => throw new InvalidOperationException($"Unsupported Portfolio command {command.GetType().Name}."),
        };
        await _events.AppendPortfolioAsync(
            state.PortfolioId,
            domainEvent,
            domainEvent.Revision - 1,
            Metadata(command, now),
            cancellationToken).ConfigureAwait(false);
        await _projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent])).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<PortfolioCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command?.ErrorCode ?? 34000, ex.Message));

    static PortfolioId ParseId(ICommand command) =>
        int.TryParse(command.Subject.EntityId, out var id) && id > 0
            ? new PortfolioId(id)
            : throw new ArgumentException("Portfolio command subject identity is invalid.");

    static PortfolioEventMetadata Metadata(ICommand command, DateTime nowUtc)
    {
        var metadata = command as IPortfolioRequestMetadata;
        return new(
            metadata is not null && metadata.CorrelationId != Guid.Empty ? metadata.CorrelationId : command.CommandId,
            command.CommandId,
            metadata is { RequestedOnUtc.Kind: DateTimeKind.Utc } ? metadata.RequestedOnUtc : nowUtc);
    }

    sealed class PortfolioActorState(PortfolioId portfolioId, PortfolioAggregate aggregate) : IActorState<PortfolioActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioId PortfolioId { get; } = portfolioId;
        public PortfolioAggregate Aggregate { get; } = aggregate;
    }
}
