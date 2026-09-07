using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using AddFundMandateVersionCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.AddFundMandateVersionPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using AssignTradeTemplateCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.AssignTradeTemplatePayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using CancelFundOrderCompositionCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.CancelFundOrderCompositionPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using ChangeFundOperatingStateCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.ChangeFundStatePayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using CreateFundMandateCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.CreateFundMandatePayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using CreateManualFundOrderCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.CreateManualFundOrderPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using ExpireFundOrderCompositionCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.ExpireFundOrderCompositionPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using MarkFundOrderComposingCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.MarkComposingPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using RecordFundOrderComposedCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.RecordComposedPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using RecordFundOrderRiskOutcomeCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.RecordRiskOutcomePayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;
using ReserveFundOrderCompositionCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.ReserveCompositionPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId>;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioFundCommandActor(
    ICommandActorContext<PortfolioFundCommandActor> context,
    IPortfolioEventStore eventStore,
    IPortfolioBusinessIdAllocator allocator,
    IEventProjector<PortfolioFundCommandActor> projector,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioFundCommandActor> logger,
    TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceQueryApi? referenceQueries = null)
    : BaseEventSourceCommandActor<PortfolioFundCommandActor>(context, logger)
{
    public const string ActorName = PortfolioCommandSubjects.FundActor;
    readonly IPortfolioEventStore _events = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    readonly IPortfolioBusinessIdAllocator _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
    readonly IEventProjector<PortfolioFundCommandActor> _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    readonly IPortfolioOperationalGuard _guard = operationalGuard ?? throw new ArgumentNullException(nameof(operationalGuard));

    protected override ValueTask OnStartup(ICommandActorContext<PortfolioFundCommandActor> context, CancellationToken cancellationToken) =>
        _projector.StartAsync(context, cancellationToken);

    protected override ValueTask OnShutdown(ICommandActorContext<PortfolioFundCommandActor> context) => _projector.StopAsync();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [PortfolioCommandVerbs.CreateFundMandate] = static message => message.AsCommand<CreateFundMandateCommand>()!,
        [PortfolioCommandVerbs.AddFundMandateVersion] = static message => message.AsCommand<AddFundMandateVersionCommand>()!,
        [PortfolioCommandVerbs.ChangeFundOperatingState] = static message => message.AsCommand<ChangeFundOperatingStateCommand>()!,
        [PortfolioCommandVerbs.AssignTradeTemplate] = static message => message.AsCommand<AssignTradeTemplateCommand>()!,
        [PortfolioCommandVerbs.ReserveFundOrderComposition] = static message => message.AsCommand<ReserveFundOrderCompositionCommand>()!,
        [PortfolioCommandVerbs.CreateManualFundOrder] = static message => message.AsCommand<CreateManualFundOrderCommand>()!,
        [PortfolioCommandVerbs.MarkFundOrderComposing] = static message => message.AsCommand<MarkFundOrderComposingCommand>()!,
        [PortfolioCommandVerbs.RecordFundOrderComposed] = static message => message.AsCommand<RecordFundOrderComposedCommand>()!,
        [PortfolioCommandVerbs.RecordFundOrderRiskOutcome] = static message => message.AsCommand<RecordFundOrderRiskOutcomeCommand>()!,
        [PortfolioCommandVerbs.CancelFundOrderComposition] = static message => message.AsCommand<CancelFundOrderCompositionCommand>()!,
        [PortfolioCommandVerbs.ExpireFundOrderComposition] = static message => message.AsCommand<ExpireFundOrderCompositionCommand>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CreateFundMandateCommand)] = command =>
            {
                var typed = (CreateFundMandateCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateCreate(errors, typed);
                return errors;
            },
            [typeof(AddFundMandateVersionCommand)] = command =>
            {
                var typed = (AddFundMandateVersionCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateVersion(errors, typed);
                return errors;
            },
            [typeof(ChangeFundOperatingStateCommand)] = command =>
            {
                var typed = (ChangeFundOperatingStateCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateStateChange(errors, typed);
                return errors;
            },
            [typeof(AssignTradeTemplateCommand)] = command =>
            {
                var typed = (AssignTradeTemplateCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateAssignment(errors, typed);
                return errors;
            },
            [typeof(ReserveFundOrderCompositionCommand)] = command =>
            {
                var typed = (ReserveFundOrderCompositionCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateReservation(errors, typed);
                return errors;
            },
            [typeof(CreateManualFundOrderCommand)] = command =>
            {
                var typed = (CreateManualFundOrderCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateManualOrder(errors, typed);
                return errors;
            },
            [typeof(MarkFundOrderComposingCommand)] = command =>
            {
                var typed = (MarkFundOrderComposingCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateMarkComposing(errors, typed);
                return errors;
            },
            [typeof(RecordFundOrderComposedCommand)] = command =>
            {
                var typed = (RecordFundOrderComposedCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateCompositionResult(errors, typed);
                return errors;
            },
            [typeof(RecordFundOrderRiskOutcomeCommand)] = command =>
            {
                var typed = (RecordFundOrderRiskOutcomeCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateRiskResult(errors, typed);
                return errors;
            },
            [typeof(CancelFundOrderCompositionCommand)] = command =>
            {
                var typed = (CancelFundOrderCompositionCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateCancel(errors, typed);
                return errors;
            },
            [typeof(ExpireFundOrderCompositionCommand)] = command =>
            {
                var typed = (ExpireFundOrderCompositionCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateExpire(errors, typed);
                return errors;
            },
        };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioFundCommandActor, ICommand, PortfolioFundActorState,
        DateTime, string, CancellationToken, ValueTask<PortfolioFundDomainEvent?>>> _receiveMap =
        new Dictionary<Type, Func<PortfolioFundCommandActor, ICommand, PortfolioFundActorState,
            DateTime, string, CancellationToken, ValueTask<PortfolioFundDomainEvent?>>>
        {
            [typeof(CreateFundMandateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioFundDomainEvent?>(((FundMandateCreated)state.Aggregate.Create(
                    command.CommandId, ((CreateFundMandateCommand)command).Payload.Mandate, now, principal)) with
                    { IdempotencyKey = ((CreateFundMandateCommand)command).Payload.IdempotencyKey }),
            [typeof(AddFundMandateVersionCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.AddVersionAsync(state, (AddFundMandateVersionCommand)command, now, principal, cancellationToken),
            [typeof(ChangeFundOperatingStateCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.ChangeStateAsync(state, (ChangeFundOperatingStateCommand)command, now, principal, cancellationToken),
            [typeof(AssignTradeTemplateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioFundDomainEvent?>(state.Aggregate.AssignTradeTemplate(
                    command.CommandId, ((AssignTradeTemplateCommand)command).Payload.ExpectedVersion,
                    ((AssignTradeTemplateCommand)command).Payload.Assignment, now, principal)),
            [typeof(ReserveFundOrderCompositionCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.ReserveAsync(state.Aggregate, (ReserveFundOrderCompositionCommand)command, now, principal, cancellationToken),
            [typeof(CreateManualFundOrderCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.CreateManualAsync(state.Aggregate, (CreateManualFundOrderCommand)command, now, principal, cancellationToken),
            [typeof(MarkFundOrderComposingCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioFundDomainEvent?>(state.Aggregate.MarkCompositionComposing(
                    command.CommandId, state.Aggregate.Revision, ((MarkFundOrderComposingCommand)command).Payload.OrderId.OrderId,
                    ((MarkFundOrderComposingCommand)command).Payload.ExpectedVersion, now, principal)),
            [typeof(RecordFundOrderComposedCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioFundDomainEvent?>(state.Aggregate.RecordCompositionResult(
                    command.CommandId, state.Aggregate.Revision, ((RecordFundOrderComposedCommand)command).Payload.OrderId.OrderId,
                    ((RecordFundOrderComposedCommand)command).Payload.ExpectedVersion,
                    ((RecordFundOrderComposedCommand)command).Payload.Result, now, principal)),
            [typeof(RecordFundOrderRiskOutcomeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioFundDomainEvent?>(state.Aggregate.RecordRiskResult(
                    command.CommandId, state.Aggregate.Revision, ((RecordFundOrderRiskOutcomeCommand)command).Payload.OrderId.OrderId,
                    ((RecordFundOrderRiskOutcomeCommand)command).Payload.ExpectedVersion,
                    ((RecordFundOrderRiskOutcomeCommand)command).Payload.Result, now, principal)),
            [typeof(CancelFundOrderCompositionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioFundDomainEvent?>(state.Aggregate.CancelComposition(
                    command.CommandId, state.Aggregate.Revision, ((CancelFundOrderCompositionCommand)command).Payload.OrderId.OrderId,
                    ((CancelFundOrderCompositionCommand)command).Payload.ExpectedVersion,
                    ((CancelFundOrderCompositionCommand)command).Payload.Reason, now, principal)),
            [typeof(ExpireFundOrderCompositionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioFundDomainEvent?>(state.Aggregate.ExpireComposition(
                    command.CommandId, state.Aggregate.Revision, ((ExpireFundOrderCompositionCommand)command).Payload.OrderId.OrderId,
                    ((ExpireFundOrderCompositionCommand)command).Payload.ExpectedVersion,
                    ((ExpireFundOrderCompositionCommand)command).Payload.Reason, now, principal)),
        };

    protected override ICommand ParseMessage(ICommandActorContext<PortfolioFundCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(threadId);
        ValidateMappedCommand(command, _validationMap);
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
        var request = (IPortfolioRequestMetadata)command;
        using var activity = PortfolioTelemetry.StartRequest("command", command.Subject.Verb, request);
        var principal = _guard.Demand(Operation(command.Subject.Verb), request, mutation: true).Principal;
        var committed = await _events.FindCommittedFundCommandAsync(state.IdValue, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (command is CreateFundMandateCommand create && committed is FundMandateCreated prior &&
                !string.Equals(PortfolioCanonicalHash.Compute(create.Payload.Mandate.DefensiveCopy()), PortfolioCanonicalHash.Compute(prior.Mandate.DefensiveCopy()), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
            await _projector.DomainEventsProjectionAsync(new DomainEventCollection([committed])).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (command is CreateFundMandateCommand requestedCreate)
        {
            var priorCreate = await _events.FindFundCreateByIdempotencyKeyAsync(state.IdValue, requestedCreate.Payload.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (priorCreate is not null)
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
        }
        var now = DateTime.UtcNow;
        var aggregate = state.Aggregate;
        await ValidateFamilyReferencesAsync(command, cancellationToken).ConfigureAwait(false);
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        var domainEvent = await receive(this, command, state, now, principal, cancellationToken).ConfigureAwait(false);
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
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", domainEvent is null ? "replayed" : "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    async Task ValidateFamilyReferencesAsync(ICommand command, CancellationToken cancellationToken)
    {
        var mandate = command switch
        {
            CreateFundMandateCommand create => create.Payload.Mandate,
            AddFundMandateVersionCommand change => change.Payload.Mandate,
            _ => null
        };
        var assignment = command is AssignTradeTemplateCommand assign ? assign.Payload.Assignment : null;
        if (mandate is { SchemaVersion: >= 3 })
        {
            if (referenceQueries is null) throw new InvalidOperationException("Fund selection lookup validation is unavailable.");
            var selections = await TomasAI.IFM.Domain.Reference.Shared.Lookups.FundSelectionCatalog.LoadAsync(referenceQueries, cancellationToken);
            selections.ValidateSelections(mandate.UnderlyingUniverse, mandate.EligibleAssetTypes, mandate.PermittedDirections, mandate.PermittedConditions);
        }
        var references = mandate?.PermittedTradeStrategyFamilies ?? (assignment?.TradeStrategyFamily is { } family ? [family] : []);
        if (references.Length == 0) return; // Read/replay compatibility for pre-v2 clients; never resolve ambiguous names here.
        if (referenceQueries is null) throw new InvalidOperationException("Reference catalog validation is unavailable.");
        foreach (var reference in references)
        {
            if (reference.CatalogDeployment is not { } key)
                throw new ArgumentException("Legacy family permissions are read-only. Select an exact ConfigurationDb deployment.");
            var row = await TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogPermissionValidation.ValidateDeploymentAsync(referenceQueries, key,
                assignment?.Enabled == true || mandate?.OperatingState == FundOperatingState.Active, cancellationToken);
            if (mandate is not null && (!mandate.PermittedTradeFamilies.Contains(row.Definition.Code, StringComparer.Ordinal) || mandate.DecisionHorizon != row.Definition.Horizon.ToString()))
                throw new ArgumentException("Fund deployment classification or horizon does not match its exact reference.");
            if (assignment is not null && (assignment.TradeFamily != row.Definition.Code || assignment.DecisionHorizon != row.Definition.Horizon.ToString() || assignment.UnderlyingUniverse.Except(row.Definition.Products.Select(p => p.Symbol), StringComparer.Ordinal).Any()))
                throw new ArgumentException("Assignment classification, horizon or product universe does not match the exact deployment.");
            if (assignment is not null)
            {
                if (assignment.TradeTemplateId != key.Id || assignment.TradeTemplateVersion != key.Version)
                    throw new ArgumentException("Assignment template identity must equal its ConfigurationDb deployment identity.");
                var selection = row.Definition.PipelineParameters.Where(x => x.Kind == TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.TradeSelection).ToArray();
                var composition = row.Definition.PipelineParameters.Where(x => x.Kind == TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.OrderComposition).ToArray();
                if (selection.Length > 1 || composition.Length > 1) throw new ArgumentException("Assignment requires one unambiguous profile per pipeline stage.");
                if (assignment.TradeSelectionHintProfileId != (selection.SingleOrDefault()?.Id ?? Guid.Empty) || assignment.TradeSelectionHintProfileVersion != (selection.SingleOrDefault()?.Version ?? 0)
                    || assignment.OrderCompositionProfileId != (composition.SingleOrDefault()?.Id ?? Guid.Empty) || assignment.OrderCompositionProfileVersion != (composition.SingleOrDefault()?.Version ?? 0))
                    throw new ArgumentException("Assignment profiles must match the exact deployment bindings.");
            }
        }
    }

    async ValueTask<PortfolioFundDomainEvent?> AddVersionAsync(
        PortfolioFundActorState state,
        AddFundMandateVersionCommand command,
        DateTime now,
        string principal,
        CancellationToken cancellationToken) =>
        state.Aggregate.AddVersion(command.CommandId, command.Payload.ExpectedVersion, command.Payload.Mandate,
            await ActivationAsync(state.IdValue, state.Aggregate, cancellationToken, command.Payload.Mandate.OperatingState == FundOperatingState.Active).ConfigureAwait(false), now, principal);

    async ValueTask<PortfolioFundDomainEvent?> ChangeStateAsync(
        PortfolioFundActorState state,
        ChangeFundOperatingStateCommand command,
        DateTime now,
        string principal,
        CancellationToken cancellationToken) =>
        state.Aggregate.ChangeState(command.CommandId, command.Payload.ExpectedVersion, command.Payload.State,
            command.Payload.Reason,
            await ActivationAsync(state.IdValue, state.Aggregate, cancellationToken, command.Payload.State == FundOperatingState.Active).ConfigureAwait(false), now, principal);

    async ValueTask<PortfolioFundDomainEvent?> CreateManualAsync(PortfolioFundAggregate aggregate,
        CreateManualFundOrderCommand command, DateTime now, string principal, CancellationToken cancellationToken)
    {
        if (aggregate.TryComposition(command.Payload.Request.IdempotencyKey, out var prior))
        {
            var hash = PortfolioCanonicalHash.Compute(command.Payload.Request);
            if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different manual draft.");
            return null;
        }
        var portfolio = await _events.LoadPortfolioAsync(new PortfolioId(command.Payload.Request.PortfolioId), cancellationToken).ConfigureAwait(false);
        if (portfolio.Current is null || portfolio.Current.PortfolioVersion != command.Payload.Request.PortfolioVersion ||
            portfolio.Current.OperatingState != PortfolioOperatingState.Active)
            throw new InvalidOperationException("Manual draft Portfolio version is stale or the Portfolio is not active.");
        var orderId = await _allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        return aggregate.CreateManualOrder(command.CommandId, command.Payload.Request, orderId, now, principal);
    }

    async ValueTask<PortfolioFundDomainEvent?> ReserveAsync(PortfolioFundAggregate aggregate,
        ReserveFundOrderCompositionCommand command, DateTime now, string principal, CancellationToken cancellationToken)
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
        return aggregate.ReserveComposition(command.CommandId, aggregate.Revision, command.Payload.Request, command.Payload.Snapshot, orderId, tradeIds, now, principal);
    }

    async ValueTask<FundActivationContext> ActivationAsync(PortfolioFundId id, PortfolioFundAggregate aggregate, CancellationToken cancellationToken, bool qualifyCatalog)
    {
        var currentAssignments = aggregate.Assignments.Where(x => x.FundMandateVersion == aggregate.Current?.FundMandateVersion).ToArray();
        if (qualifyCatalog && referenceQueries is not null)
            foreach (var assignment in currentAssignments.Where(x => x.Enabled))
            {
                var deployment = assignment.TradeStrategyFamily?.CatalogDeployment ?? throw new InvalidOperationException("Legacy assignments must be replaced before activating a Fund.");
                await TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogPermissionValidation.ValidateDeploymentAsync(referenceQueries, deployment, true, cancellationToken);
            }

        var portfolio = await _events.LoadPortfolioAsync(new PortfolioId(id.PortfolioId), cancellationToken).ConfigureAwait(false);
        var enabled = currentAssignments.Count(x => x.Enabled);
        return new(portfolio.Current?.OperatingState == PortfolioOperatingState.Active, enabled,
            currentAssignments.Any(x => x.Enabled && x.TradeSelectionHintProfileId != Guid.Empty),
            currentAssignments.Any(x => x.Enabled && x.OrderCompositionProfileId != Guid.Empty));
    }

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(ErrorCode(command, ex), ex.Message));

    static int ErrorCode(ICommand? command, Exception exception) => exception switch
    {
        PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
        PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
        _ => command?.ErrorCode ?? 34100,
    };

    static PortfolioOperation Operation(string verb) => verb switch
    {
        PortfolioCommandVerbs.AssignTradeTemplate => PortfolioOperation.AssignTemplate,
        PortfolioCommandVerbs.ReserveFundOrderComposition or
            PortfolioCommandVerbs.MarkFundOrderComposing or
            PortfolioCommandVerbs.ExpireFundOrderComposition => PortfolioOperation.ReserveComposition,
        PortfolioCommandVerbs.RecordFundOrderComposed => PortfolioOperation.RecordCompositionResult,
        PortfolioCommandVerbs.RecordFundOrderRiskOutcome => PortfolioOperation.RecordRiskResult,
        _ => PortfolioOperation.AdministerFund,
    };

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

    static void ValidateIdentity<TPayload>(
        List<ValidationError> errors,
        PortfolioCommand<TPayload, PortfolioFundId> command)
    {
        if (command.EntityId is null)
        {
            if (command.Payload is null)
                errors.Add(new($"{command.CommandName}.Payload is null"));
            return;
        }
        AddErrors(errors, command.EntityId.Validate(), command.CommandName);
        if (!string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal))
            errors.Add(new($"{command.CommandName}.EntityId does not match Subject.EntityId"));
        if (command.Payload is null)
            errors.Add(new($"{command.CommandName}.Payload is null"));
    }

    static void ValidateCreate(List<ValidationError> errors, CreateFundMandateCommand command)
    {
        if (command.Payload is null) return;
        if (command.Payload.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Payload.IdempotencyKey is empty"));
        ValidateMandate(errors, command, command.Payload.Mandate);
    }

    static void ValidateVersion(List<ValidationError> errors, AddFundMandateVersionCommand command)
    {
        if (command.Payload is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        ValidateMandate(errors, command, command.Payload.Mandate);
    }

    static void ValidateStateChange(List<ValidationError> errors, ChangeFundOperatingStateCommand command)
    {
        if (command.Payload is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        if (command.Payload.State == FundOperatingState.Unknown)
            errors.Add(new($"{command.CommandName}.Payload.State is required"));
        ValidateReason(errors, command.Payload.Reason, command.CommandName);
    }

    static void ValidateAssignment(List<ValidationError> errors, AssignTradeTemplateCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        if (command.Payload.Assignment is null)
        {
            errors.Add(new($"{command.CommandName}.Payload.Assignment is null"));
            return;
        }
        if (command.Payload.Assignment.UnderlyingUniverse is null)
            errors.Add(new($"{command.CommandName}.Payload.Assignment.UnderlyingUniverse is null"));
        else
            AddErrors(errors, command.Payload.Assignment.Validate(), command.CommandName);
        if (command.Payload.Assignment.PortfolioId != command.EntityId.PortfolioId ||
            command.Payload.Assignment.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Payload.Assignment identity does not match EntityId"));
    }

    static void ValidateReservation(List<ValidationError> errors, ReserveFundOrderCompositionCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        var request = command.Payload.Request;
        var snapshot = command.Payload.Snapshot;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Payload.Request is null"));
            return;
        }
        if (snapshot is null)
        {
            errors.Add(new($"{command.CommandName}.Payload.Snapshot is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId.PortfolioId || request.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Payload.Request identity does not match EntityId"));
        if (request.WorkflowId == Guid.Empty || request.WorkflowRevision <= 0 ||
            request.TradeSelectionInvocationId == Guid.Empty || request.TradeSelectionResultId == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Payload.Request workflow identity is invalid"));
        if (request.PortfolioVersion <= 0 || request.FundMandateVersion <= 0 ||
            request.TradeTemplateId == Guid.Empty || request.TradeTemplateVersion <= 0 ||
            request.OrderCompositionProfileId == Guid.Empty || request.OrderCompositionProfileVersion <= 0)
            errors.Add(new($"{command.CommandName}.Payload.Request versioned identities are invalid"));
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.TradeSelectionResultSha256) ||
            string.IsNullOrWhiteSpace(request.PortfolioFundStrategySnapshotSha256))
            errors.Add(new($"{command.CommandName}.Payload.Request hashes and idempotency key are required"));
        if (string.IsNullOrWhiteSpace(request.UnderlyingRoot) || string.IsNullOrWhiteSpace(request.DecisionHorizon) ||
            request.TradeInstructions is null || request.TradeInstructions.Length == 0 ||
            request.TradeInstructions.Any(static instruction => instruction is null))
            errors.Add(new($"{command.CommandName}.Payload.Request trade instructions are required"));
        ValidateUtcWindow(errors, request.RequestedAtUtc, request.ExpiresAtUtc, command.CommandName);
        if (snapshot.Portfolio is null || snapshot.Fund is null || snapshot.Allocation is null ||
            snapshot.RiskEnvelope is null || snapshot.FinancialPolicy is null || snapshot.Assignments is null ||
            snapshot.Assignments.Any(static assignment => assignment is null))
            errors.Add(new($"{command.CommandName}.Payload.Snapshot contains null values"));
        else if (snapshot.WorkflowId != request.WorkflowId || snapshot.WorkflowRevision != request.WorkflowRevision ||
                 snapshot.Portfolio.PortfolioId != request.PortfolioId || snapshot.Fund.PortfolioId != request.PortfolioId ||
                 snapshot.Fund.FundId != request.FundId)
            errors.Add(new($"{command.CommandName}.Payload.Snapshot does not match Request"));
        if (snapshot.ResolvedAtUtc.Kind != DateTimeKind.Utc || snapshot.ValidUntilUtc.Kind != DateTimeKind.Utc ||
            snapshot.ValidUntilUtc <= snapshot.ResolvedAtUtc || string.IsNullOrWhiteSpace(snapshot.PayloadSha256))
            errors.Add(new($"{command.CommandName}.Payload.Snapshot validity is invalid"));
    }

    static void ValidateManualOrder(List<ValidationError> errors, CreateManualFundOrderCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        var request = command.Payload.Request;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Payload.Request is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId.PortfolioId || request.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Payload.Request identity does not match EntityId"));
        if (request.PortfolioVersion <= 0 || request.FundMandateVersion <= 0 || request.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Payload.Request version and idempotency values are invalid"));
        if (string.IsNullOrWhiteSpace(request.UnderlyingRoot))
            errors.Add(new($"{command.CommandName}.Payload.Request.UnderlyingRoot is required"));
        ValidateUtcWindow(errors, request.RequestedAtUtc, request.ExpiresAtUtc, command.CommandName);
    }

    static void ValidateMarkComposing(List<ValidationError> errors, MarkFundOrderComposingCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateOrderId(errors, command.Payload.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        if (command.Payload.InvocationId == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Payload.InvocationId is empty"));
    }

    static void ValidateCompositionResult(List<ValidationError> errors, RecordFundOrderComposedCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateOrderId(errors, command.Payload.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        var result = command.Payload.Result;
        if (result is null || result.ResultId == Guid.Empty || result.InvocationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(result.ResultSha256))
            errors.Add(new($"{command.CommandName}.Payload.Result identity is invalid"));
        else
            ValidateUtcWindow(errors, result.EvaluatedAtUtc, result.ExpiresAtUtc, command.CommandName);
    }

    static void ValidateRiskResult(List<ValidationError> errors, RecordFundOrderRiskOutcomeCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateOrderId(errors, command.Payload.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        var result = command.Payload.Result;
        if (result is null || result.ResultId == Guid.Empty || result.EnvelopeId == Guid.Empty ||
            result.EnvelopeVersion <= 0 || result.Decision == RiskDecision.Unknown ||
            string.IsNullOrWhiteSpace(result.ResultSha256) || string.IsNullOrWhiteSpace(result.CandidateSha256))
            errors.Add(new($"{command.CommandName}.Payload.Result identity is invalid"));
        else
            ValidateUtcWindow(errors, result.EvaluatedAtUtc, result.ExpiresAtUtc, command.CommandName);
    }

    static void ValidateCancel(List<ValidationError> errors, CancelFundOrderCompositionCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateOrderId(errors, command.Payload.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Payload.Reason, command.CommandName);
    }

    static void ValidateExpire(List<ValidationError> errors, ExpireFundOrderCompositionCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateOrderId(errors, command.Payload.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Payload.Reason, command.CommandName);
    }

    static void ValidateMandate<TPayload>(
        List<ValidationError> errors,
        PortfolioCommand<TPayload, PortfolioFundId> command,
        FundMandateReadModel? mandate)
    {
        if (mandate is null)
        {
            errors.Add(new($"{command.CommandName}.Payload.Mandate is null"));
            return;
        }
        if (mandate.UnderlyingUniverse is null || mandate.EligibleAssetTypes is null ||
            mandate.PermittedDirections is null || mandate.PermittedConditions is null ||
            mandate.PermittedTradeFamilies is null)
            errors.Add(new($"{command.CommandName}.Payload.Mandate contains null collections"));
        else
            AddErrors(errors, mandate.Validate(), command.CommandName);
        if (command.EntityId is null)
            return;
        if (mandate.PortfolioId != command.EntityId.PortfolioId || mandate.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Payload.Mandate identity does not match EntityId"));
    }

    static void ValidateOrderId(
        List<ValidationError> errors,
        PortfolioFundOrderId? orderId,
        PortfolioFundId entityId,
        string commandName)
    {
        if (orderId is null)
        {
            errors.Add(new($"{commandName}.Payload.OrderId is null"));
            return;
        }
        AddErrors(errors, orderId.Validate(), commandName);
        if (orderId.PortfolioId != entityId.PortfolioId || orderId.FundId != entityId.FundId)
            errors.Add(new($"{commandName}.Payload.OrderId parent identity does not match EntityId"));
    }

    static void ValidateExpectedVersion(List<ValidationError> errors, long expectedVersion, string commandName)
    {
        if (expectedVersion < 0)
            errors.Add(new($"{commandName}.Payload.ExpectedVersion cannot be negative"));
    }

    static void ValidateReason(List<ValidationError> errors, string? reason, string commandName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            errors.Add(new($"{commandName}.Payload.Reason is required"));
    }

    static void ValidateUtcWindow(List<ValidationError> errors, DateTime start, DateTime end, string commandName)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || end <= start)
            errors.Add(new($"{commandName}.Payload time window must contain ordered UTC values"));
    }

    static void AddErrors(List<ValidationError> errors, IEnumerable<string> messages, string commandName)
    {
        foreach (var message in messages)
            errors.Add(new($"{commandName}.{message}"));
    }

    sealed class PortfolioFundActorState(PortfolioFundId id, PortfolioFundAggregate aggregate) : IActorState<PortfolioFundActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioFundId IdValue { get; } = id;
        public PortfolioFundAggregate Aggregate { get; } = aggregate;
    }
}
