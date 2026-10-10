using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Fund.Command.Validation;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
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



namespace TomasAI.IFM.Domain.Portfolio.Fund.Command.Actor;

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
    readonly TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceQueryApi? _referenceQueries = referenceQueries;

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

            [RecordFundTradeSubmissionCommand.Verb] = static message => message.AsCommand<RecordFundTradeSubmissionCommand>()!,
            [RecordFundTradeOpeningCommand.Verb] = static message => message.AsCommand<RecordFundTradeOpeningCommand>()!,
            [RecordFundTradeClosingCommand.Verb] = static message => message.AsCommand<RecordFundTradeClosingCommand>()!,
            [ReleaseFundTradeSubmissionCommand.Verb] = static message => message.AsCommand<ReleaseFundTradeSubmissionCommand>()!,
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
            [typeof(SynchronizeFundRiskOutcomeCommand)] = static command =>
            {
                var typed = (SynchronizeFundRiskOutcomeCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(AuthorizeFundOrderRiskCommand)] = static command =>
            {
                var typed = (AuthorizeFundOrderRiskCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(CreateFundMandateCommand)] = static command =>
            {
                var typed = (CreateFundMandateCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(AddFundMandateVersionCommand)] = static command =>
            {
                var typed = (AddFundMandateVersionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(ChangeFundOperatingStateCommand)] = static command =>
            {
                var typed = (ChangeFundOperatingStateCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(AssignTradeTemplateCommand)] = static command =>
            {
                var typed = (AssignTradeTemplateCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(ReserveFundOrderCompositionCommand)] = static command =>
            {
                var typed = (ReserveFundOrderCompositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(CreateManualFundOrderCommand)] = static command =>
            {
                var typed = (CreateManualFundOrderCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(AddManualFundOrderTradeCommand)] = static command =>
            {
                var typed = (AddManualFundOrderTradeCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(RemoveManualFundOrderTradeCommand)] = static command =>
            {
                var typed = (RemoveManualFundOrderTradeCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(RecordFundTradeSubmissionCommand)] = static command =>
            {
                var typed = (RecordFundTradeSubmissionCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName).ValidateFundCommand(typed);
            },
            [typeof(RecordFundTradeOpeningCommand)] = static command =>
            {
                var typed = (RecordFundTradeOpeningCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName).ValidateFundCommand(typed);
            },
            [typeof(RecordFundTradeClosingCommand)] = static command =>
            {
                var typed = (RecordFundTradeClosingCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName).ValidateFundCommand(typed);
            },
            [typeof(ReleaseFundTradeSubmissionCommand)] = static command =>
            {
                var typed = (ReleaseFundTradeSubmissionCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName).ValidateFundCommand(typed);
            },
            [typeof(DeleteManualFundOrderCommand)] = static command =>
            {
                var typed = (DeleteManualFundOrderCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(MarkFundOrderComposingCommand)] = static command =>
            {
                var typed = (MarkFundOrderComposingCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(RecordFundOrderComposedCommand)] = static command =>
            {
                var typed = (RecordFundOrderComposedCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(RecordFundOrderRiskOutcomeCommand)] = static command =>
            {
                var typed = (RecordFundOrderRiskOutcomeCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(CancelFundOrderCompositionCommand)] = static command =>
            {
                var typed = (CancelFundOrderCompositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
            [typeof(ExpireFundOrderCompositionCommand)] = static command =>
            {
                var typed = (ExpireFundOrderCompositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFundCommand(typed);
            },
        };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioFundCommandActor, ICommand, PortfolioFundActorState,
        DateTime, string, CancellationToken, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<PortfolioFundCommandActor, ICommand, PortfolioFundActorState,
            DateTime, string, CancellationToken, ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(AuthorizeFundOrderRiskCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<ServiceResult<GuidResult>>(((AuthorizeFundOrderRiskCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(CreateFundMandateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((CreateFundMandateCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(AddFundMandateVersionCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                ((AddFundMandateVersionCommand)command).ExecuteAsync(state.IdValue, state.Aggregate, actor._events, actor._referenceQueries, now, principal, cancellationToken),
            [typeof(ChangeFundOperatingStateCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                ((ChangeFundOperatingStateCommand)command).ExecuteAsync(state.IdValue, state.Aggregate, actor._events, actor._referenceQueries, now, principal, cancellationToken),
            [typeof(AssignTradeTemplateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((AssignTradeTemplateCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(ReserveFundOrderCompositionCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                ((ReserveFundOrderCompositionCommand)command).ExecuteAsync(state.Aggregate, actor._allocator, now, principal, cancellationToken),
            [typeof(CreateManualFundOrderCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                ((CreateManualFundOrderCommand)command).ExecuteAsync(state.Aggregate, actor._events, actor._allocator, now, principal, cancellationToken),
            [typeof(AddManualFundOrderTradeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((AddManualFundOrderTradeCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(RemoveManualFundOrderTradeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((RemoveManualFundOrderTradeCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(RecordFundTradeSubmissionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((RecordFundTradeSubmissionCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(RecordFundTradeOpeningCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((RecordFundTradeOpeningCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(RecordFundTradeClosingCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((RecordFundTradeClosingCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(ReleaseFundTradeSubmissionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((ReleaseFundTradeSubmissionCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(DeleteManualFundOrderCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((DeleteManualFundOrderCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(MarkFundOrderComposingCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((MarkFundOrderComposingCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(RecordFundOrderComposedCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((RecordFundOrderComposedCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(SynchronizeFundRiskOutcomeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((SynchronizeFundRiskOutcomeCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(RecordFundOrderRiskOutcomeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((RecordFundOrderRiskOutcomeCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(CancelFundOrderCompositionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((CancelFundOrderCompositionCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(ExpireFundOrderCompositionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((ExpireFundOrderCompositionCommand)command).Execute(state.Aggregate, now, principal)),
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
        var principal = _guard.Demand(Operation(command.Subject.Verb), request.Access, mutation: true).Principal;
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
        var acceptance = await receive(this, command, state, now, principal, cancellationToken).ConfigureAwait(false);
        if (!acceptance.Success) return acceptance;
        var domainEvent = state.Aggregate.PendingEvent;
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
            if (_referenceQueries is null) throw new InvalidOperationException("Fund selection lookup validation is unavailable.");
            var selections = await TomasAI.IFM.Domain.Reference.Shared.Lookups.FundSelectionCatalog.LoadAsync(_referenceQueries, cancellationToken);
            selections.ValidateSelections(mandate.UnderlyingUniverse, mandate.EligibleAssetTypes, mandate.PermittedDirections, mandate.PermittedConditions);
        }
        var references = mandate?.PermittedTradeStrategyFamilies ?? (assignment?.TradeStrategyFamily is { } family ? [family] : []);
        if (references.Length == 0) return; // Read/replay compatibility for pre-v2 clients; never resolve ambiguous names here.
        if (_referenceQueries is null) throw new InvalidOperationException("Reference catalog validation is unavailable.");
        foreach (var reference in references)
        {
            if (reference.CatalogDeployment is not { } key)
                throw new ArgumentException("Legacy family permissions are read-only. Select an exact ConfigurationDb deployment.");
            var row = await TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogPermissionValidation.ValidateDeploymentAsync(_referenceQueries, key,
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
        RecordFundTradeSubmissionCommand.Verb or RecordFundTradeOpeningCommand.Verb or RecordFundTradeClosingCommand.Verb or ReleaseFundTradeSubmissionCommand.Verb => PortfolioOperation.RecordCompositionResult,
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

    sealed class PortfolioFundActorState(PortfolioFundId id, PortfolioFundAggregate aggregate) : IActorState<PortfolioFundActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioFundId IdValue { get; } = id;
        public PortfolioFundAggregate Aggregate { get; } = aggregate;
    }
}
