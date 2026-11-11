using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Fund.Command.Validation;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
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
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.Validation;



namespace TomasAI.IFM.Domain.Portfolio.Fund.Command.Actor;

public sealed class PortfolioFundCommandActor(
    ICommandActorContext<PortfolioFundCommandActor> context,
    IPortfolioEventStore eventStore,
    IEventProjector<PortfolioFundCommandActor> projector,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioFundCommandActor> logger, TimeProvider? timeProvider = null)
    : BaseEventSourceCommandActor<PortfolioFundCommandActor>(context, logger)
{
    readonly TimeProvider businessClock = timeProvider ?? TimeProvider.System;
    public const string ActorName = CreateFundMandateCommand.Actor;
    readonly IPortfolioEventStore _events = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
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
            [ConfigureFundBaseContractsCommand.Verb] = static message => message.AsCommand<ConfigureFundBaseContractsCommand>()!,
            [AddFundMandateVersionCommand.Verb] = static message => message.AsCommand<AddFundMandateVersionCommand>()!,
            [ChangeFundOperatingStateCommand.Verb] = static message => message.AsCommand<ChangeFundOperatingStateCommand>()!,
            [AssignTradeTemplateCommand.Verb] = static message => message.AsCommand<AssignTradeTemplateCommand>()!,
            [ReserveFundOrderCompositionCommand.Verb] = static message => message.AsCommand<ReserveFundOrderCompositionCommand>()!,
            [CreateManualFundOrderCommand.Verb] = static message => message.AsCommand<CreateManualFundOrderCommand>()!,
            [AddManualFundOrderTradeCommand.Verb] = static message => message.AsCommand<AddManualFundOrderTradeCommand>()!,
            [RemoveManualFundOrderTradeCommand.Verb] = static message => message.AsCommand<RemoveManualFundOrderTradeCommand>()!,

            [RecordFundTradeSubmissionCommand.Verb] = static message => message.AsCommand<RecordFundTradeSubmissionCommand>()!,
            [RecordFundTradeOpeningCommand.Verb] = static message => message.AsCommand<RecordFundTradeOpeningCommand>()!,
            [RegisterStrategyFundOrderCommand.Verb] = static message => message.AsCommand<RegisterStrategyFundOrderCommand>()!,
            [ReleaseExpiredStrategyFundOrderCommand.Verb] = static message => message.AsCommand<ReleaseExpiredStrategyFundOrderCommand>()!,
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
            [typeof(ConfigureFundBaseContractsCommand)] = static command =>
            {
                var typed = (ConfigureFundBaseContractsCommand)command;
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
            [typeof(RegisterStrategyFundOrderCommand)] = static command =>
            {
                var typed = (RegisterStrategyFundOrderCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateEntityId(typed.EntityId, typed.CommandName);
            },
            [typeof(ReleaseExpiredStrategyFundOrderCommand)] = static command =>
            {
                var typed = (ReleaseExpiredStrategyFundOrderCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateEntityId(typed.EntityId, typed.CommandName);
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

    static readonly IReadOnlyDictionary<Type, Func<ICommand, PortfolioFundActorState, DateTime, string, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, PortfolioFundActorState, DateTime, string, ServiceResult<GuidResult>>>
        {
            [typeof(AuthorizeFundOrderRiskCommand)] = static (command, state, now, principal) =>
                ((AuthorizeFundOrderRiskCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(CreateFundMandateCommand)] = static (command, state, now, principal) =>
                ((CreateFundMandateCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(ConfigureFundBaseContractsCommand)] = static (command, state, now, principal) =>
                ((ConfigureFundBaseContractsCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(AddFundMandateVersionCommand)] = static (command, state, now, principal) =>
                ((AddFundMandateVersionCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(ChangeFundOperatingStateCommand)] = static (command, state, now, principal) =>
                ((ChangeFundOperatingStateCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(AssignTradeTemplateCommand)] = static (command, state, now, principal) =>
                ((AssignTradeTemplateCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(ReserveFundOrderCompositionCommand)] = static (command, state, now, principal) =>
                ((ReserveFundOrderCompositionCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(CreateManualFundOrderCommand)] = static (command, state, now, principal) =>
                ((CreateManualFundOrderCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(AddManualFundOrderTradeCommand)] = static (command, state, now, principal) =>
                ((AddManualFundOrderTradeCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(RemoveManualFundOrderTradeCommand)] = static (command, state, now, principal) =>
                ((RemoveManualFundOrderTradeCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(RecordFundTradeSubmissionCommand)] = static (command, state, now, principal) =>
                ((RecordFundTradeSubmissionCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(RecordFundTradeOpeningCommand)] = static (command, state, now, principal) =>
                ((RecordFundTradeOpeningCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(RegisterStrategyFundOrderCommand)] = static (command, state, now, principal) =>
                ((RegisterStrategyFundOrderCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(ReleaseExpiredStrategyFundOrderCommand)] = static (command, state, now, principal) =>
                ((ReleaseExpiredStrategyFundOrderCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(RecordFundTradeClosingCommand)] = static (command, state, now, principal) =>
                ((RecordFundTradeClosingCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(ReleaseFundTradeSubmissionCommand)] = static (command, state, now, principal) =>
                ((ReleaseFundTradeSubmissionCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(DeleteManualFundOrderCommand)] = static (command, state, now, principal) =>
                ((DeleteManualFundOrderCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(MarkFundOrderComposingCommand)] = static (command, state, now, principal) =>
                ((MarkFundOrderComposingCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(RecordFundOrderComposedCommand)] = static (command, state, now, principal) =>
                ((RecordFundOrderComposedCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(SynchronizeFundRiskOutcomeCommand)] = static (command, state, now, principal) =>
                ((SynchronizeFundRiskOutcomeCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(RecordFundOrderRiskOutcomeCommand)] = static (command, state, now, principal) =>
                ((RecordFundOrderRiskOutcomeCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(CancelFundOrderCompositionCommand)] = static (command, state, now, principal) =>
                ((CancelFundOrderCompositionCommand)command).Execute(state.Aggregate, now, principal),
            [typeof(ExpireFundOrderCompositionCommand)] = static (command, state, now, principal) =>
                ((ExpireFundOrderCompositionCommand)command).Execute(state.Aggregate, now, principal),
        };

    protected override ICommand ParseMessage(ICommandActorContext<PortfolioFundCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, _parseMap);

    protected override async ValueTask OnValidateAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateMappedCommand(command, _validationMap);
        if (command is RegisterStrategyFundOrderCommand registration &&
            !await context.Container.Resolve<TomasAI.IFM.Domain.Portfolio.Fund.Command.StrategyFundOrderRegistrationProof>().VerifyAsync(registration).ConfigureAwait(false))
            throw new InvalidOperationException("STRATEGY_FUND_ORDER.AUTHORIZATION_SOURCE_MISMATCH");
        if (command is ReleaseExpiredStrategyFundOrderCommand expiry &&
            !await context.Container.Resolve<TomasAI.IFM.Domain.Portfolio.Fund.Command.StrategyFundOrderRegistrationProof>().VerifyExpiryAsync(expiry).ConfigureAwait(false))
            throw new InvalidOperationException("STRATEGY_FUND_EXPIRY.RELEASE_SOURCE_MISMATCH");
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var id = ParseId(command);
        return new PortfolioFundActorState(id, await _events.LoadFundAsync(id).ConfigureAwait(false));
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioFundCommandActor> context, IActorState state, ICommand command) 
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        return ReceiveCoreAsync((PortfolioFundActorState)state, command, CancellationToken.None);
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioFundCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken) 
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        return ReceiveCoreAsync((PortfolioFundActorState)state, command, cancellationToken);
    }

    async ValueTask<ServiceResult<GuidResult>> ReceiveCoreAsync(PortfolioFundActorState state, ICommand command, CancellationToken cancellationToken)
    {
        var request = (IPortfolioLifecycleCommand)command;
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
            if (command is ConfigureFundBaseContractsCommand config &&
                (committed is not FundBaseContractsConfiguredEvent configured ||
                TomasAI.IFM.Domain.Portfolio.Workflow.PortfolioCanonicalHash.Compute(config.Mandate) !=
                TomasAI.IFM.Domain.Portfolio.Workflow.PortfolioCanonicalHash.Compute(configured.Mandate)))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict,"Base Contracts CommandId was used for different settings.");
            if (command is CreateFundMandateCommand create && committed is FundMandateCreatedEvent prior &&
                !string.Equals(PortfolioCanonicalHash.Compute(create.Mandate.DefensiveCopy()), PortfolioCanonicalHash.Compute(prior.Mandate.DefensiveCopy()), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
            using (PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.project"))
                await Projection.PortfolioProjectionSubmission.SubmitAsync(_projector, committed, Logger).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (command is CreateFundMandateCommand requestedCreate)
        {
            var priorCreate = await _events.FindFundCreateByIdempotencyKeyAsync(state.IdValue, requestedCreate.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (priorCreate is not null)
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Fund mandate payload.");
        }
        var now = businessClock.GetUtcNow().UtcDateTime;
        _=state.Aggregate;
        await VerifyReferencePreparationAsync(command, state.Aggregate, cancellationToken).ConfigureAwait(false);
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        using var transitionTrace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.transition");
        var acceptance = receive(command, state, now, principal);
        if (!acceptance.Success) return acceptance;
        var domainEvent = state.Aggregate.PendingEvent;
        transitionTrace?.Stop();
        if (domainEvent is not null)
        {
            TomasAI.IFM.Domain.Portfolio.Projection.PortfolioAcceptedProjection.Capture(domainEvent, state.Aggregate);
            await _events.AppendFundWithStateAsync(
                state.IdValue,
                domainEvent,
                domainEvent.Revision - 1,
                state.Aggregate,
                Metadata(command, now),
                cancellationToken).ConfigureAwait(false);
            using (PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.project"))
                await Projection.PortfolioProjectionSubmission.SubmitAsync(_projector, domainEvent, Logger).ConfigureAwait(false);
        }
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", domainEvent is null ? "replayed" : "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    async Task VerifyReferencePreparationAsync(ICommand command, PortfolioFundAggregate aggregate, CancellationToken cancellationToken)
    {
        var request = ValidatePortfolioReferenceCommand.ForOwnerCommand(command.CommandId) with { Access = ((IPortfolioLifecycleCommand)command).Access };
        if (ActivationPreparation(command) is { } activation)
        {
            if (activation.ParentPortfolioVersion <= 0 || activation.ParentSourceRevision <= 0 ||
                command is not (ChangeFundOperatingStateCommand { State: FundOperatingState.Active } or
                    AddFundMandateVersionCommand { Mandate.OperatingState: FundOperatingState.Active }))
                throw new InvalidOperationException("Invalid parent Portfolio activation preparation.");
            request = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request, request with { ActivationAssignments = FundActivationQualification.EnabledAssignments(aggregate) });
        }
        (ValidatePortfolioReferenceCommand? Input, Guid? Receipt) prepared = command switch
        {
            CreateFundMandateCommand create => (global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request, request with { Mandate = create.Mandate }), create.ReferencePreparationId),
            ConfigureFundBaseContractsCommand change => (global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request, request with { Mandate = change.Mandate }), change.ReferencePreparationId),
            AddFundMandateVersionCommand change => (global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request, request with { Mandate = change.Mandate }), change.ReferencePreparationId),
            AssignTradeTemplateCommand assign => (global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request, request with { Assignment = assign.Assignment }), assign.ReferencePreparationId),
            ChangeFundOperatingStateCommand change when change.ActivationPreparation is not null || change.ReferencePreparationId is not null
                => (request, change.ReferencePreparationId),
            _ => (null, null)
        };
        if (prepared.Input is null) return;
        if (prepared.Receipt is null)
        {
            if (prepared.Input.RequiresEvidence)
                throw new InvalidOperationException("The Fund command requires a committed Reference validation receipt.");
            return;
        }
        var receipt = ValidatePortfolioReferenceCommand.ForOwnerCommand(command.CommandId, prepared.Receipt);
        await _events.VerifyReferencePreparationAsync(global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(prepared.Input, prepared.Input with
            { CommandId = receipt.CommandId, Subject = receipt.Subject, EntityId = receipt.EntityId }), cancellationToken).ConfigureAwait(false);
    }

    static FundActivationPreparation? ActivationPreparation(ICommand command) => command switch
    {
        AddFundMandateVersionCommand change => change.ActivationPreparation,
        ChangeFundOperatingStateCommand change => change.ActivationPreparation,
        _ => null
    };

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<PortfolioFundCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) 
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(ErrorCode(command, ex), ex.Message));
    }

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
        RecordFundTradeSubmissionCommand.Verb or RecordFundTradeOpeningCommand.Verb or RecordFundTradeClosingCommand.Verb or ReleaseFundTradeSubmissionCommand.Verb or RegisterStrategyFundOrderCommand.Verb or ReleaseExpiredStrategyFundOrderCommand.Verb => PortfolioOperation.RecordCompositionResult,
        _ => PortfolioOperation.AdministerFund,
    };

    static PortfolioEventMetadata Metadata(ICommand command, DateTime nowUtc)
    {
        var metadata = (IPortfolioLifecycleCommand)command;
        var manual = command as CreateManualFundOrderCommand;
        var prepared = manual?.Preparation ?? (command as ReserveFundOrderCompositionCommand)?.Preparation;
        var activation = ActivationPreparation(command);
        Guid correlationId = metadata.CorrelationId;
        DateTime requestedOnUtc = metadata.RequestedOnUtc;
        return new(correlationId != Guid.Empty ? correlationId : command.CommandId, command.CommandId,
            requestedOnUtc.Kind == DateTimeKind.Utc ? requestedOnUtc : nowUtc)
        {
            ExpectedPortfolioRevision = manual?.Preparation?.ParentSourceRevision ?? activation?.ParentSourceRevision,
            ExpectedPortfolioVersion = manual?.Preparation?.Parent?.PortfolioVersion ?? activation?.ParentPortfolioVersion,
            ExpectedPortfolioOperatingState = manual?.Preparation is null && activation is null ? null : PortfolioOperatingState.Active,
            PreparedIdentifiers = prepared is null ? null : new(ParseId(command), command.CommandId, metadata.Access, prepared.OrderId, prepared.TradeIds)
        };
    }

    static PortfolioFundId ParseId(ICommand command)
    {
        var parts = command.Subject.EntityId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0],System.Globalization.CultureInfo.InvariantCulture, out var portfolioId) && int.TryParse(parts[1],System.Globalization.CultureInfo.InvariantCulture, out var fundId) && portfolioId > 0 && fundId > 0
            ? new PortfolioFundId(portfolioId, fundId)
            : throw new ArgumentException("PortfolioFund command subject identity is invalid.",nameof(command));
    }

    sealed class PortfolioFundActorState(PortfolioFundId id, PortfolioFundAggregate aggregate) : IActorState<PortfolioFundActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioFundId IdValue { get; } = id;
        public PortfolioFundAggregate Aggregate { get; } = aggregate;
    }
}
