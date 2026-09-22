using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
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
    public const string ActorName = CreateFundMandateCommand.Actor;
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
        [SynchronizeFundRiskOutcomeCommand.Verb] = static message => message.AsCommand<SynchronizeFundRiskOutcomeCommand>()!,
        [AuthorizeFundOrderRiskCommand.Verb] = static message => message.AsCommand<AuthorizeFundOrderRiskCommand>()!,
        [CreateFundMandateCommand.Verb] = static message => message.AsCommand<CreateFundMandateCommand>()!,
        [AddFundMandateVersionCommand.Verb] = static message => message.AsCommand<AddFundMandateVersionCommand>()!,
        [ChangeFundOperatingStateCommand.Verb] = static message => message.AsCommand<ChangeFundOperatingStateCommand>()!,
        [AssignTradeTemplateCommand.Verb] = static message => message.AsCommand<AssignTradeTemplateCommand>()!,
        [ReserveFundOrderCompositionCommand.Verb] = static message => message.AsCommand<ReserveFundOrderCompositionCommand>()!,
        [CreateManualFundOrderCommand.Verb] = static message => message.AsCommand<CreateManualFundOrderCommand>()!,
        [AddManualFundOrderTradeCommand.Verb] = static message => message.AsCommand<AddManualFundOrderTradeCommand>()!,
        [RemoveManualFundOrderTradeCommand.Verb] = static message => message.AsCommand<RemoveManualFundOrderTradeCommand>()!,
        [ChangeManualFundOrderTradeStateCommand.Verb] = static message => message.AsCommand<ChangeManualFundOrderTradeStateCommand>()!,
        [CloseManualFundOrderCommand.Verb] = static message => message.AsCommand<CloseManualFundOrderCommand>()!,
        [DeleteManualFundOrderCommand.Verb] = static message => message.AsCommand<DeleteManualFundOrderCommand>()!,
        [MarkFundOrderComposingCommand.Verb] = static message => message.AsCommand<MarkFundOrderComposingCommand>()!,
        [RecordFundOrderComposedCommand.Verb] = static message => message.AsCommand<RecordFundOrderComposedCommand>()!,
        [RecordFundOrderRiskOutcomeCommand.Verb] = static message => message.AsCommand<RecordFundOrderRiskOutcomeCommand>()!,
        [CancelFundOrderCompositionCommand.Verb] = static message => message.AsCommand<CancelFundOrderCompositionCommand>()!,
        [ExpireFundOrderCompositionCommand.Verb] = static message => message.AsCommand<ExpireFundOrderCompositionCommand>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(SynchronizeFundRiskOutcomeCommand)] = command =>
            {
                var typed = (SynchronizeFundRiskOutcomeCommand)command;
                var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                if (typed.Evidence is not { } e || typed.ExpectedVersion <= 0 || e.PortfolioId != typed.EntityId.PortfolioId || e.FundId != typed.EntityId.FundId)
                    errors.Add(new("Exact terminal evidence and Fund version are required."));
                return errors;
            },
            [typeof(AuthorizeFundOrderRiskCommand)] = command =>
            {
                var typed = (AuthorizeFundOrderRiskCommand)command;
                var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName).ValidateFundRiskAuthorization(typed);
                ValidateIdentity(errors, typed);
                return errors;
            },
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
            [typeof(AddManualFundOrderTradeCommand)] = command =>
            {
                var typed = (AddManualFundOrderTradeCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateManualTrade(errors, typed);
                return errors;
            },
            [typeof(RemoveManualFundOrderTradeCommand)] = command =>
            {
                var typed = (RemoveManualFundOrderTradeCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateManualTradeMutation(errors, typed.Request, typed.EntityId, typed.CommandName, false);
                return errors;
            },
            [typeof(ChangeManualFundOrderTradeStateCommand)] = command =>
            {
                var typed = (ChangeManualFundOrderTradeStateCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateManualTradeMutation(errors, typed.Request, typed.EntityId, typed.CommandName, true);
                return errors;
            },
            [typeof(CloseManualFundOrderCommand)] = command =>
            {
                var typed = (CloseManualFundOrderCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateManualOrderMutation(errors, typed.Request, typed.EntityId, typed.CommandName);
                return errors;
            },
            [typeof(DeleteManualFundOrderCommand)] = command =>
            {
                var typed = (DeleteManualFundOrderCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateManualOrderMutation(errors, typed.Request, typed.EntityId, typed.CommandName);
                return errors;
            },            [typeof(MarkFundOrderComposingCommand)] = command =>
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
        DateTime, string, CancellationToken, ValueTask<IPortfolioFundDomainEvent?>>> _receiveMap =
        new Dictionary<Type, Func<PortfolioFundCommandActor, ICommand, PortfolioFundActorState,
            DateTime, string, CancellationToken, ValueTask<IPortfolioFundDomainEvent?>>>
        {
            [typeof(AuthorizeFundOrderRiskCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(((AuthorizeFundOrderRiskCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(CreateFundMandateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(((FundMandateCreatedEvent)state.Aggregate.Create(
                    command.CommandId, ((CreateFundMandateCommand)command).Mandate, now, principal)) with
                    { IdempotencyKey = ((CreateFundMandateCommand)command).IdempotencyKey }),
            [typeof(AddFundMandateVersionCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.AddVersionAsync(state, (AddFundMandateVersionCommand)command, now, principal, cancellationToken),
            [typeof(ChangeFundOperatingStateCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.ChangeStateAsync(state, (ChangeFundOperatingStateCommand)command, now, principal, cancellationToken),
            [typeof(AssignTradeTemplateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.AssignTradeTemplate(
                    command.CommandId, ((AssignTradeTemplateCommand)command).ExpectedVersion,
                    ((AssignTradeTemplateCommand)command).Assignment, now, principal)),
            [typeof(ReserveFundOrderCompositionCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.ReserveAsync(state.Aggregate, (ReserveFundOrderCompositionCommand)command, now, principal, cancellationToken),
            [typeof(CreateManualFundOrderCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.CreateManualAsync(state.Aggregate, (CreateManualFundOrderCommand)command, now, principal, cancellationToken),
            [typeof(AddManualFundOrderTradeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.AddManualTrade(
                    command.CommandId, ((AddManualFundOrderTradeCommand)command).Request, now, principal)),
            [typeof(RemoveManualFundOrderTradeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.RemoveManualTrade(
                    command.CommandId, ((RemoveManualFundOrderTradeCommand)command).Request, now, principal)),
            [typeof(ChangeManualFundOrderTradeStateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.ChangeManualTradeState(
                    command.CommandId, ((ChangeManualFundOrderTradeStateCommand)command).Request, now, principal)),
            [typeof(CloseManualFundOrderCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.CloseManualOrder(
                    command.CommandId, ((CloseManualFundOrderCommand)command).Request, now, principal)),
            [typeof(DeleteManualFundOrderCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.DeleteManualOrder(
                    command.CommandId, ((DeleteManualFundOrderCommand)command).Request, now, principal)),            [typeof(MarkFundOrderComposingCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.MarkCompositionComposing(
                    command.CommandId, state.Aggregate.Revision, ((MarkFundOrderComposingCommand)command).OrderId.OrderId,
                    ((MarkFundOrderComposingCommand)command).ExpectedVersion, now, principal)),
            [typeof(RecordFundOrderComposedCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.RecordCompositionResult(
                    command.CommandId, state.Aggregate.Revision, ((RecordFundOrderComposedCommand)command).OrderId.OrderId,
                    ((RecordFundOrderComposedCommand)command).ExpectedVersion,
                    ((RecordFundOrderComposedCommand)command).Result, now, principal)),
            [typeof(SynchronizeFundRiskOutcomeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.SynchronizeRisk(command.CommandId,
                    ((SynchronizeFundRiskOutcomeCommand)command).ExpectedVersion, ((SynchronizeFundRiskOutcomeCommand)command).Evidence, now, principal)),
            [typeof(RecordFundOrderRiskOutcomeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.RecordRiskResult(
                    command.CommandId, state.Aggregate.Revision, ((RecordFundOrderRiskOutcomeCommand)command).OrderId.OrderId,
                    ((RecordFundOrderRiskOutcomeCommand)command).ExpectedVersion,
                    ((RecordFundOrderRiskOutcomeCommand)command).Result, now, principal)),
            [typeof(CancelFundOrderCompositionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.CancelComposition(
                    command.CommandId, state.Aggregate.Revision, ((CancelFundOrderCompositionCommand)command).OrderId.OrderId,
                    ((CancelFundOrderCompositionCommand)command).ExpectedVersion,
                    ((CancelFundOrderCompositionCommand)command).Reason, now, principal)),
            [typeof(ExpireFundOrderCompositionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioFundDomainEvent?>(state.Aggregate.ExpireComposition(
                    command.CommandId, state.Aggregate.Revision, ((ExpireFundOrderCompositionCommand)command).OrderId.OrderId,
                    ((ExpireFundOrderCompositionCommand)command).ExpectedVersion,
                    ((ExpireFundOrderCompositionCommand)command).Reason, now, principal)),
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
        dynamic request = command;
        using var activity = PortfolioTelemetry.StartRequest("command", command.Subject.Verb, request.CorrelationId, command);
        var principal = _guard.Demand(Operation(command.Subject.Verb), request, mutation: true).Principal;
        var committed = await _events.FindCommittedFundCommandAsync(state.IdValue, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (command is SynchronizeFundRiskOutcomeCommand sync &&
                (committed is not FundCompositionStateChangedEvent terminal || terminal.Order.TerminalRisk != sync.Evidence || terminal.Order.AggregateVersion != sync.ExpectedVersion + 1))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "Terminal CommandId was used for different input.");
            if (command is AuthorizeFundOrderRiskCommand authorization &&
                (committed is not FundCompositionStateChangedEvent authorized || authorized.Order.RiskAuthorization != authorization.Authorization ||
                 authorized.Order.OrderId != authorization.OrderId.OrderId || authorized.Order.AggregateVersion != authorization.ExpectedVersion + 1))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "Authorization CommandId was used for different input.");
            if (command is CreateFundMandateCommand create && committed is FundMandateCreatedEvent prior &&
                !string.Equals(PortfolioCanonicalHash.Compute(create.Mandate.DefensiveCopy()), PortfolioCanonicalHash.Compute(prior.Mandate.DefensiveCopy()), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
            using (PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.project"))
                await _projector.DomainEventsProjectionAsync(new DomainEventCollection([committed])).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (command is CreateFundMandateCommand requestedCreate)
        {
            var priorCreate = await _events.FindFundCreateByIdempotencyKeyAsync(state.IdValue, requestedCreate.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (priorCreate is not null)
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
        }
        var now = DateTime.UtcNow;
        var aggregate = state.Aggregate;
        await ValidateFamilyReferencesAsync(command, cancellationToken).ConfigureAwait(false);
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        using var transitionTrace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.transition");
        var domainEvent = await receive(this, command, state, now, principal, cancellationToken).ConfigureAwait(false);
        transitionTrace?.Stop();
        if (domainEvent is not null)
        {
            await _events.AppendFundAsync(
                state.IdValue,
                domainEvent,
                domainEvent.Revision - 1,
                Metadata(command, now),
                cancellationToken).ConfigureAwait(false);
            using (PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.project"))
                await _projector.DomainEventsProjectionAsync(new DomainEventCollection(new IEvent[] { domainEvent })).ConfigureAwait(false);
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
            CreateFundMandateCommand create => create.Mandate,
            AddFundMandateVersionCommand change => change.Mandate,
            _ => null
        };
        var assignment = command is AssignTradeTemplateCommand assign ? assign.Assignment : null;
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

    async ValueTask<IPortfolioFundDomainEvent?> AddVersionAsync(
        PortfolioFundActorState state,
        AddFundMandateVersionCommand command,
        DateTime now,
        string principal,
        CancellationToken cancellationToken) =>
        state.Aggregate.AddVersion(command.CommandId, command.ExpectedVersion, command.Mandate,
            await ActivationAsync(state.IdValue, state.Aggregate, cancellationToken, command.Mandate.OperatingState == FundOperatingState.Active).ConfigureAwait(false), now, principal);

    async ValueTask<IPortfolioFundDomainEvent?> ChangeStateAsync(
        PortfolioFundActorState state,
        ChangeFundOperatingStateCommand command,
        DateTime now,
        string principal,
        CancellationToken cancellationToken) =>
        state.Aggregate.ChangeState(command.CommandId, command.ExpectedVersion, command.State,
            command.Reason,
            await ActivationAsync(state.IdValue, state.Aggregate, cancellationToken, command.State == FundOperatingState.Active).ConfigureAwait(false), now, principal);

    async ValueTask<IPortfolioFundDomainEvent?> CreateManualAsync(PortfolioFundAggregate aggregate,
        CreateManualFundOrderCommand command, DateTime now, string principal, CancellationToken cancellationToken)
    {
        if (aggregate.TryComposition(command.Request.IdempotencyKey, out var prior))
        {
            var hash = PortfolioCanonicalHash.Compute(command.Request);
            if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different manual draft.");
            return null;
        }
        var portfolio = await _events.LoadPortfolioAsync(new PortfolioId(command.Request.PortfolioId), cancellationToken).ConfigureAwait(false);
        if (portfolio.Current is null || portfolio.Current.PortfolioVersion != command.Request.PortfolioVersion ||
            portfolio.Current.OperatingState != PortfolioOperatingState.Active)
            throw new InvalidOperationException("Manual draft Portfolio version is stale or the Portfolio is not active.");
        var orderId = await _allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        return aggregate.CreateManualOrder(command.CommandId, command.Request, orderId, now, principal);
    }

    async ValueTask<IPortfolioFundDomainEvent?> ReserveAsync(PortfolioFundAggregate aggregate,
        ReserveFundOrderCompositionCommand command, DateTime now, string principal, CancellationToken cancellationToken)
    {
        if (aggregate.TryComposition(command.Request.IdempotencyKey, out var prior))
        {
            var hash = PortfolioCanonicalHash.Compute(command.Request.DefensiveCopy());
            if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different canonical request.");
            return null;
        }
        var orderId = await _allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        var tradeIds = new int[command.Request.TradeInstructions.Length];
        for (var i = 0; i < tradeIds.Length; i++) tradeIds[i] = await _allocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false);
        return aggregate.ReserveComposition(command.CommandId, aggregate.Revision, command.Request, command.Snapshot, orderId, tradeIds, now, principal);
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
        AssignTradeTemplateCommand.Verb => PortfolioOperation.AssignTemplate,
        ReserveFundOrderCompositionCommand.Verb or
            MarkFundOrderComposingCommand.Verb or
            ExpireFundOrderCompositionCommand.Verb => PortfolioOperation.ReserveComposition,
        RecordFundOrderComposedCommand.Verb => PortfolioOperation.RecordCompositionResult,
        SynchronizeFundRiskOutcomeCommand.Verb or RecordFundOrderRiskOutcomeCommand.Verb or AuthorizeFundOrderRiskCommand.Verb => PortfolioOperation.RecordRiskResult,
        _ => PortfolioOperation.AdministerFund,
    };

    static PortfolioEventMetadata Metadata(ICommand command, DateTime nowUtc)
    {
        dynamic metadata = command;
        Guid correlationId = metadata.CorrelationId;
        DateTime requestedOnUtc = metadata.RequestedOnUtc;
        return new(correlationId != Guid.Empty ? correlationId : command.CommandId, command.CommandId,
            requestedOnUtc.Kind == DateTimeKind.Utc ? requestedOnUtc : nowUtc);
    }

    static PortfolioFundId ParseId(ICommand command)
    {
        var parts = command.Subject.EntityId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var portfolioId) && int.TryParse(parts[1], out var fundId) && portfolioId > 0 && fundId > 0
            ? new PortfolioFundId(portfolioId, fundId)
            : throw new ArgumentException("PortfolioFund command subject identity is invalid.");
    }

    static void ValidateIdentity(
        List<ValidationError> errors,
        ICommand<PortfolioFundId> command)
    {
        if (command.EntityId is null)
        {
            return;
        }
        AddErrors(errors, command.EntityId.Validate(), command.CommandName);
        if (!string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal))
            errors.Add(new($"{command.CommandName}.EntityId does not match Subject.EntityId"));
    }

    static void ValidateCreate(List<ValidationError> errors, CreateFundMandateCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.IdempotencyKey is empty"));
        ValidateMandate(errors, command, command.Mandate);
    }

    static void ValidateVersion(List<ValidationError> errors, AddFundMandateVersionCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateMandate(errors, command, command.Mandate);
    }

    static void ValidateStateChange(List<ValidationError> errors, ChangeFundOperatingStateCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.State == FundOperatingState.Unknown)
            errors.Add(new($"{command.CommandName}.State is required"));
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidateAssignment(List<ValidationError> errors, AssignTradeTemplateCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.Assignment is null)
        {
            errors.Add(new($"{command.CommandName}.Assignment is null"));
            return;
        }
        if (command.Assignment.UnderlyingUniverse is null)
            errors.Add(new($"{command.CommandName}.Assignment.UnderlyingUniverse is null"));
        else
            AddErrors(errors, command.Assignment.Validate(), command.CommandName);
        if (command.Assignment.PortfolioId != command.EntityId.PortfolioId ||
            command.Assignment.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Assignment identity does not match EntityId"));
    }

    static void ValidateReservation(List<ValidationError> errors, ReserveFundOrderCompositionCommand command)
    {
        var request = command.Request;
        var snapshot = command.Snapshot;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Request is null"));
            return;
        }
        if (snapshot is null)
        {
            errors.Add(new($"{command.CommandName}.Snapshot is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId.PortfolioId || request.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Request identity does not match EntityId"));
        if (request.WorkflowId == Guid.Empty || request.WorkflowRevision <= 0 ||
            request.TradeSelectionInvocationId == Guid.Empty || request.TradeSelectionResultId == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Request workflow identity is invalid"));
        if (request.PortfolioVersion <= 0 || request.FundMandateVersion <= 0 ||
            request.TradeTemplateId == Guid.Empty || request.TradeTemplateVersion <= 0 ||
            request.OrderCompositionProfileId == Guid.Empty || request.OrderCompositionProfileVersion <= 0)
            errors.Add(new($"{command.CommandName}.Request versioned identities are invalid"));
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.TradeSelectionResultSha256) ||
            string.IsNullOrWhiteSpace(request.PortfolioFundStrategySnapshotSha256))
            errors.Add(new($"{command.CommandName}.Request hashes and idempotency key are required"));
        if (string.IsNullOrWhiteSpace(request.UnderlyingRoot) || string.IsNullOrWhiteSpace(request.DecisionHorizon) ||
            request.TradeInstructions is null || request.TradeInstructions.Length == 0 ||
            request.TradeInstructions.Any(static instruction => instruction is null))
            errors.Add(new($"{command.CommandName}.Request trade instructions are required"));
        ValidateUtcWindow(errors, request.RequestedAtUtc, request.ExpiresAtUtc, command.CommandName);
        if (snapshot.Portfolio is null || snapshot.Fund is null || snapshot.Allocation is null ||
            snapshot.RiskEnvelope is null || snapshot.FinancialPolicy is null || snapshot.Assignments is null ||
            snapshot.Assignments.Any(static assignment => assignment is null))
            errors.Add(new($"{command.CommandName}.Snapshot contains null values"));
        else if (snapshot.WorkflowId != request.WorkflowId || snapshot.WorkflowRevision != request.WorkflowRevision ||
                 snapshot.Portfolio.PortfolioId != request.PortfolioId || snapshot.Fund.PortfolioId != request.PortfolioId ||
                 snapshot.Fund.FundId != request.FundId)
            errors.Add(new($"{command.CommandName}.Snapshot does not match Request"));
        if (snapshot.ResolvedAtUtc.Kind != DateTimeKind.Utc || snapshot.ValidUntilUtc.Kind != DateTimeKind.Utc ||
            snapshot.ValidUntilUtc <= snapshot.ResolvedAtUtc || string.IsNullOrWhiteSpace(snapshot.PayloadSha256))
            errors.Add(new($"{command.CommandName}.Snapshot validity is invalid"));
    }

    static void ValidateManualOrder(List<ValidationError> errors, CreateManualFundOrderCommand command)
    {
        var request = command.Request;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Request is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId.PortfolioId || request.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Request identity does not match EntityId"));
        if (request.PortfolioVersion <= 0 || request.FundMandateVersion <= 0 || request.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Request version and idempotency values are invalid"));
        if (string.IsNullOrWhiteSpace(request.UnderlyingRoot))
            errors.Add(new($"{command.CommandName}.Request.UnderlyingRoot is required"));
        ValidateUtcWindow(errors, request.RequestedAtUtc, request.ExpiresAtUtc, command.CommandName);
    }

    static void ValidateManualTrade(List<ValidationError> errors, AddManualFundOrderTradeCommand command)
    {
        var request = command.Request;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Request is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId.PortfolioId || request.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Request identity does not match EntityId"));
        if (request.OrderId <= 0 || request.ExpectedOrderVersion <= 0 || request.TradeId <= 0)
            errors.Add(new($"{command.CommandName}.Request order and trade identities are invalid"));
        if (string.IsNullOrWhiteSpace(request.TradeType) ||
            string.IsNullOrWhiteSpace(request.TradeState) ||
            string.IsNullOrWhiteSpace(request.TradeAction) ||
            string.IsNullOrWhiteSpace(request.Reference) ||
            string.IsNullOrWhiteSpace(request.BaseContractSymbol))
            errors.Add(new($"{command.CommandName}.Request trade fields are required"));
        if (request.RequestedAtUtc.Kind != DateTimeKind.Utc)
            errors.Add(new($"{command.CommandName}.Request.RequestedAtUtc must be UTC"));
    }

    static void ValidateManualTradeMutation(
        List<ValidationError> errors,
        ManualFundOrderTradeMutationRequest? request,
        PortfolioFundId? entityId,
        string commandName,
        bool requireState)
    {
        if (request is null)
        {
            errors.Add(new($"{commandName}.Request is null"));
            return;
        }
        if (entityId is not null &&
            (request.PortfolioId != entityId.PortfolioId || request.FundId != entityId.FundId))
            errors.Add(new($"{commandName}.Request identity does not match EntityId"));
        if (request.OrderId <= 0 || request.ExpectedOrderVersion <= 0 || request.TradeId <= 0)
            errors.Add(new($"{commandName}.Request order and trade identities are invalid"));
        if (requireState && string.IsNullOrWhiteSpace(request.TradeState))
            errors.Add(new($"{commandName}.Request.TradeState is required"));
        if (request.RequestedAtUtc.Kind != DateTimeKind.Utc)
            errors.Add(new($"{commandName}.Request.RequestedAtUtc must be UTC"));
    }

    static void ValidateManualOrderMutation(
        List<ValidationError> errors,
        ManualFundOrderMutationRequest? request,
        PortfolioFundId? entityId,
        string commandName)
    {
        if (request is null)
        {
            errors.Add(new($"{commandName}.Request is null"));
            return;
        }
        if (entityId is not null &&
            (request.PortfolioId != entityId.PortfolioId || request.FundId != entityId.FundId))
            errors.Add(new($"{commandName}.Request identity does not match EntityId"));
        if (request.OrderId <= 0 || request.ExpectedOrderVersion <= 0)
            errors.Add(new($"{commandName}.Request order identity is invalid"));
        if (request.RequestedAtUtc.Kind != DateTimeKind.Utc)
            errors.Add(new($"{commandName}.Request.RequestedAtUtc must be UTC"));
    }

    static void ValidateMarkComposing(List<ValidationError> errors, MarkFundOrderComposingCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.InvocationId == Guid.Empty)
            errors.Add(new($"{command.CommandName}.InvocationId is empty"));
    }

    static void ValidateCompositionResult(List<ValidationError> errors, RecordFundOrderComposedCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        var result = command.Result;
        if (result is null || result.ResultId == Guid.Empty || result.InvocationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(result.ResultSha256))
            errors.Add(new($"{command.CommandName}.Result identity is invalid"));
        else
            ValidateUtcWindow(errors, result.EvaluatedAtUtc, result.ExpiresAtUtc, command.CommandName);
    }

    static void ValidateRiskResult(List<ValidationError> errors, RecordFundOrderRiskOutcomeCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        var result = command.Result;
        if (result is null || result.ResultId == Guid.Empty || result.EnvelopeId == Guid.Empty ||
            result.EnvelopeVersion <= 0 || result.Decision == RiskDecision.Unknown ||
            string.IsNullOrWhiteSpace(result.ResultSha256) || string.IsNullOrWhiteSpace(result.CandidateSha256))
            errors.Add(new($"{command.CommandName}.Result identity is invalid"));
        else
            ValidateUtcWindow(errors, result.EvaluatedAtUtc, result.ExpiresAtUtc, command.CommandName);
    }

    static void ValidateCancel(List<ValidationError> errors, CancelFundOrderCompositionCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidateExpire(List<ValidationError> errors, ExpireFundOrderCompositionCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidateMandate(
        List<ValidationError> errors,
        ICommand<PortfolioFundId> command,
        FundMandateReadModel? mandate)
    {
        if (mandate is null)
        {
            errors.Add(new($"{command.CommandName}.Mandate is null"));
            return;
        }
        if (mandate.UnderlyingUniverse is null || mandate.EligibleAssetTypes is null ||
            mandate.PermittedDirections is null || mandate.PermittedConditions is null ||
            mandate.PermittedTradeFamilies is null)
            errors.Add(new($"{command.CommandName}.Mandate contains null collections"));
        else
            AddErrors(errors, mandate.Validate(), command.CommandName);
        if (command.EntityId is null)
            return;
        if (mandate.PortfolioId != command.EntityId.PortfolioId || mandate.FundId != command.EntityId.FundId)
            errors.Add(new($"{command.CommandName}.Mandate identity does not match EntityId"));
    }

    static void ValidateOrderId(
        List<ValidationError> errors,
        PortfolioFundOrderId? orderId,
        PortfolioFundId entityId,
        string commandName)
    {
        if (orderId is null)
        {
            errors.Add(new($"{commandName}.OrderId is null"));
            return;
        }
        AddErrors(errors, orderId.Validate(), commandName);
        if (orderId.PortfolioId != entityId.PortfolioId || orderId.FundId != entityId.FundId)
            errors.Add(new($"{commandName}.OrderId parent identity does not match EntityId"));
    }

    static void ValidateExpectedVersion(List<ValidationError> errors, long expectedVersion, string commandName)
    {
        if (expectedVersion < 0)
            errors.Add(new($"{commandName}.ExpectedVersion cannot be negative"));
    }

    static void ValidateReason(List<ValidationError> errors, string? reason, string commandName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            errors.Add(new($"{commandName}.Reason is required"));
    }

    static void ValidateUtcWindow(List<ValidationError> errors, DateTime start, DateTime end, string commandName)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || end <= start)
            errors.Add(new($"{commandName}.time window must contain ordered UTC values"));
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
