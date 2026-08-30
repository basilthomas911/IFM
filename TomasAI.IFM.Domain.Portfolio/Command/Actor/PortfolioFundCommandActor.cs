using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioFundCommandActor(
    ICommandActorContext<PortfolioFundCommandActor> context,
    IPortfolioEventStore eventStore,
    IPortfolioBusinessIdAllocator allocator,
    IEventProjector<PortfolioFundCommandActor> projector,
    ILogger<PortfolioFundCommandActor> logger)
    : BaseEventSourceCommandActor<PortfolioFundCommandActor>(context, logger)
{
    public const string ActorName = PortfolioCommandSubjects.FundActor;
    const string Principal = "portfolio-fund-nats";
    readonly IPortfolioEventStore _events = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    readonly IPortfolioBusinessIdAllocator _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
    readonly IEventProjector<PortfolioFundCommandActor> _projector = projector ?? throw new ArgumentNullException(nameof(projector));

    protected override ValueTask OnStartup(ICommandActorContext<PortfolioFundCommandActor> context, CancellationToken cancellationToken) =>
        _projector.StartAsync(context, cancellationToken);

    protected override ValueTask OnShutdown(ICommandActorContext<PortfolioFundCommandActor> context) => _projector.StopAsync();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap = new Dictionary<string, Func<IActorMessage, ICommand>>
    {
        ["CreateFundMandate"] = x => x.AsCommand<PortfolioCommand<CreateFundMandatePayload, PortfolioFundId>>()!,
        ["AddFundMandateVersion"] = x => x.AsCommand<PortfolioCommand<AddFundMandateVersionPayload, PortfolioFundId>>()!,
        ["ChangeFundOperatingState"] = x => x.AsCommand<PortfolioCommand<ChangeFundStatePayload, PortfolioFundId>>()!,
        ["AssignTradeTemplate"] = x => x.AsCommand<PortfolioCommand<AssignTradeTemplatePayload, PortfolioFundId>>()!,
        ["ReserveFundOrderComposition"] = x => x.AsCommand<PortfolioCommand<ReserveCompositionPayload, PortfolioFundId>>()!,
        ["MarkFundOrderComposing"] = x => x.AsCommand<PortfolioCommand<MarkComposingPayload, PortfolioFundId>>()!,
        ["RecordFundOrderComposed"] = x => x.AsCommand<PortfolioCommand<RecordComposedPayload, PortfolioFundId>>()!,
        ["RecordFundOrderRiskOutcome"] = x => x.AsCommand<PortfolioCommand<RecordRiskOutcomePayload, PortfolioFundId>>()!,
        ["CancelFundOrderComposition"] = x => x.AsCommand<PortfolioCommand<StopCompositionPayload, PortfolioFundId>>()!,
        ["ExpireFundOrderComposition"] = x => x.AsCommand<PortfolioCommand<StopCompositionPayload, PortfolioFundId>>()!,
    };

    protected override ICommand ParseMessage(ICommandActorContext<PortfolioFundCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, ParseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        if (command.CommandId == Guid.Empty || ParseId(command).Validate().Count != 0)
            throw new ArgumentException("A valid PortfolioFund command identity is required.");
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        var id = ParseId(command);
        return new PortfolioFundActorState(id, await _events.LoadFundAsync(id).ConfigureAwait(false));
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioFundCommandActor> context, IActorState state, ICommand command) =>
        ReceiveCoreAsync((PortfolioFundActorState)state, command, CancellationToken.None);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioFundCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken) =>
        ReceiveCoreAsync((PortfolioFundActorState)state, command, cancellationToken);

    async ValueTask<ServiceResult<GuidResult>> ReceiveCoreAsync(PortfolioFundActorState state, ICommand command, CancellationToken cancellationToken)
    {
        var committed = await _events.FindCommittedFundCommandAsync(state.IdValue, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (command is PortfolioCommand<CreateFundMandatePayload, PortfolioFundId> create && committed is FundMandateCreated prior &&
                !string.Equals(PortfolioCanonicalHash.Compute(create.Payload.Mandate.DefensiveCopy()), PortfolioCanonicalHash.Compute(prior.Mandate.DefensiveCopy()), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (command is PortfolioCommand<CreateFundMandatePayload, PortfolioFundId> requestedCreate)
        {
            var priorCreate = await _events.FindFundCreateByIdempotencyKeyAsync(state.IdValue, requestedCreate.Payload.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (priorCreate is not null)
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
        }
        var now = DateTime.UtcNow;
        var aggregate = state.Aggregate;
        PortfolioFundDomainEvent? domainEvent = command switch
        {
            PortfolioCommand<CreateFundMandatePayload, PortfolioFundId> x => ((FundMandateCreated)aggregate.Create(x.CommandId, x.Payload.Mandate, now, Principal)) with { IdempotencyKey = x.Payload.IdempotencyKey },
            PortfolioCommand<AddFundMandateVersionPayload, PortfolioFundId> x => aggregate.AddVersion(x.CommandId, x.Payload.ExpectedVersion, x.Payload.Mandate, await ActivationAsync(state.IdValue, aggregate, cancellationToken), now, Principal),
            PortfolioCommand<ChangeFundStatePayload, PortfolioFundId> x => aggregate.ChangeState(x.CommandId, x.Payload.ExpectedVersion, x.Payload.State, x.Payload.Reason, await ActivationAsync(state.IdValue, aggregate, cancellationToken), now, Principal),
            PortfolioCommand<AssignTradeTemplatePayload, PortfolioFundId> x => aggregate.AssignTradeTemplate(x.CommandId, x.Payload.ExpectedVersion, x.Payload.Assignment, now, Principal),
            PortfolioCommand<MarkComposingPayload, PortfolioFundId> x => aggregate.MarkCompositionComposing(x.CommandId, aggregate.Revision, x.Payload.OrderId.OrderId, x.Payload.ExpectedVersion, now, Principal),
            PortfolioCommand<RecordComposedPayload, PortfolioFundId> x => aggregate.RecordCompositionResult(x.CommandId, aggregate.Revision, x.Payload.OrderId.OrderId, x.Payload.ExpectedVersion, x.Payload.Result, now, Principal),
            PortfolioCommand<RecordRiskOutcomePayload, PortfolioFundId> x => aggregate.RecordRiskResult(x.CommandId, aggregate.Revision, x.Payload.OrderId.OrderId, x.Payload.ExpectedVersion, x.Payload.Result, now, Principal),
            PortfolioCommand<StopCompositionPayload, PortfolioFundId> x when command.Subject.Verb == "CancelFundOrderComposition" => aggregate.CancelComposition(x.CommandId, aggregate.Revision, x.Payload.OrderId.OrderId, x.Payload.ExpectedVersion, x.Payload.Reason, now, Principal),
            PortfolioCommand<StopCompositionPayload, PortfolioFundId> x => aggregate.ExpireComposition(x.CommandId, aggregate.Revision, x.Payload.OrderId.OrderId, x.Payload.ExpectedVersion, x.Payload.Reason, now, Principal),
            PortfolioCommand<ReserveCompositionPayload, PortfolioFundId> x => await ReserveAsync(aggregate, x, now, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported PortfolioFund command {command.GetType().Name}."),
        };
        if (domainEvent is not null)
        {
            await _events.AppendFundAsync(
                state.IdValue,
                domainEvent,
                domainEvent.Revision - 1,
                Metadata(command, now),
                cancellationToken).ConfigureAwait(false);
            await _projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent])).ConfigureAwait(false);
        }
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    async ValueTask<PortfolioFundDomainEvent?> ReserveAsync(PortfolioFundAggregate aggregate,
        PortfolioCommand<ReserveCompositionPayload, PortfolioFundId> command, DateTime now, CancellationToken cancellationToken)
    {
        if (aggregate.TryComposition(command.Payload.Request.IdempotencyKey, out var prior))
        {
            var hash = PortfolioCanonicalHash.Compute(command.Payload.Request.DefensiveCopy());
            if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different canonical request.");
            return null;
        }
        var orderId = await _allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        var tradeIds = new int[command.Payload.Request.TradeInstructions.Length];
        for (var i = 0; i < tradeIds.Length; i++) tradeIds[i] = await _allocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false);
        return aggregate.ReserveComposition(command.CommandId, aggregate.Revision, command.Payload.Request, command.Payload.Snapshot, orderId, tradeIds, now, Principal);
    }

    async ValueTask<FundActivationContext> ActivationAsync(PortfolioFundId id, PortfolioFundAggregate aggregate, CancellationToken cancellationToken)
    {
        var portfolio = await _events.LoadPortfolioAsync(new PortfolioId(id.PortfolioId), cancellationToken).ConfigureAwait(false);
        var enabled = aggregate.Assignments.Count(x => x.Enabled);
        return new(portfolio.Current?.OperatingState == PortfolioOperatingState.Active, enabled,
            aggregate.Assignments.Any(x => x.Enabled && x.TradeSelectionHintProfileId != Guid.Empty),
            aggregate.Assignments.Any(x => x.Enabled && x.OrderCompositionProfileId != Guid.Empty));
    }

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command?.ErrorCode ?? 34100, ex.Message));

    static PortfolioEventMetadata Metadata(ICommand command, DateTime nowUtc)
    {
        var metadata = command as IPortfolioRequestMetadata;
        return new(
            metadata is not null && metadata.CorrelationId != Guid.Empty ? metadata.CorrelationId : command.CommandId,
            command.CommandId,
            metadata is { RequestedOnUtc.Kind: DateTimeKind.Utc } ? metadata.RequestedOnUtc : nowUtc);
    }

    static PortfolioFundId ParseId(ICommand command)
    {
        var parts = command.Subject.EntityId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var portfolioId) && int.TryParse(parts[1], out var fundId) && portfolioId > 0 && fundId > 0
            ? new PortfolioFundId(portfolioId, fundId)
            : throw new ArgumentException("PortfolioFund command subject identity is invalid.");
    }

    sealed class PortfolioFundActorState(PortfolioFundId id, PortfolioFundAggregate aggregate) : IActorState<PortfolioFundActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioFundId IdValue { get; } = id;
        public PortfolioFundAggregate Aggregate { get; } = aggregate;
    }
}
