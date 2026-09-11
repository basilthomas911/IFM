using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using System.Collections.Frozen;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;

/// <summary>Owns atomic snapshot transitions for one Intrinsic Time Strategy Workflow entity.</summary>
/// <remarks>
/// Every accepted transition appends only <see cref="WorkflowStrategyStateUpdatedEvent"/>. Pipeline work is never
/// dispatched here; the conventional projector may publish the committed snapshot after PostgreSQL succeeds.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowCommandActor(
    ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> actorContext)
    : BaseEventSourceCommandActor<IntrinsicTimeStrategyWorkflowCommandActor>(actorContext, actorContext.Logger)
{
    static readonly IReadOnlyDictionary<Type,Func<ICommand,CancellationToken,ValueTask<bool>>> _duplicateRetryMap =
        new Dictionary<Type,Func<ICommand,CancellationToken,ValueTask<bool>>>
        {
            [typeof(AdvanceRiskFinancialHandoffCommand)] = static (command,token)=>((AdvanceRiskFinancialHandoffCommand)command).ResumeAfterAuditAsync(token),
            [typeof(PrepareRiskManagementCommand)] = static (command,token)=>((PrepareRiskManagementCommand)command).ResumeAfterAuditAsync(token)
        }.ToFrozenDictionary();

    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        ICommand command,CancellationToken token)
        =>_duplicateRetryMap.TryGetValue(command.GetType(),out var handler) ? handler(command,token) : ValueTask.FromResult(false);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [AdvanceRiskFinancialHandoffCommand.Verb] = message=>message.AsCommand<AdvanceRiskFinancialHandoffCommand>()!,
            [PrepareRiskManagementCommand.Verb] = message => message.AsCommand<PrepareRiskManagementCommand>()!,
            [AcceptOrderCompositionPreparationCommand.Verb] = message => message.AsCommand<AcceptOrderCompositionPreparationCommand>()!,
            [ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb] =
                message => message.AsCommand<ExecuteIntrinsicTimeStrategyWorkflowCommand>()!,
            [CompleteRegimeDiscoveryCommand.Verb] = message => message.AsCommand<CompleteRegimeDiscoveryCommand>()!,
            [CompleteMarketConditionCommand.Verb] = message => message.AsCommand<CompleteMarketConditionCommand>()!,
            [RedispatchCurrentStrategyPipelineCommand.Verb] = message => message.AsCommand<RedispatchCurrentStrategyPipelineCommand>()!,
            [CompleteTradeSelectionReservationCommand.Verb] = message => message.AsCommand<CompleteTradeSelectionReservationCommand>()!,
            [CompleteTradeSelectionCommand.Verb] = message => message.AsCommand<CompleteTradeSelectionCommand>()!,
            [CompleteOrderCompositionCommand.Verb] = message => message.AsCommand<CompleteOrderCompositionCommand>()!,
            [CompleteRiskManagementCommand.Verb] = message => message.AsCommand<CompleteRiskManagementCommand>()!,
            [FailRegimeDiscoveryCommand.Verb] = message => message.AsCommand<FailRegimeDiscoveryCommand>()!,
            [FailMarketConditionCommand.Verb] = message => message.AsCommand<FailMarketConditionCommand>()!,
            [FailTradeSelectionCommand.Verb] = message => message.AsCommand<FailTradeSelectionCommand>()!,
            [FailOrderCompositionCommand.Verb] = message => message.AsCommand<FailOrderCompositionCommand>()!,
            [FailRiskManagementCommand.Verb] = message => message.AsCommand<FailRiskManagementCommand>()!,
            [TimeoutMarketConditionCommand.Verb] = message => message.AsCommand<TimeoutMarketConditionCommand>()!,
            [TimeoutTradeSelectionCommand.Verb] = message => message.AsCommand<TimeoutTradeSelectionCommand>()!,
            [TimeoutOrderCompositionCommand.Verb] = message => message.AsCommand<TimeoutOrderCompositionCommand>()!,
            [TimeoutRiskManagementCommand.Verb] = message => message.AsCommand<TimeoutRiskManagementCommand>()!,
            [CancelIntrinsicTimeStrategyWorkflowCommand.Verb] =
                message => message.AsCommand<CancelIntrinsicTimeStrategyWorkflowCommand>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(AdvanceRiskFinancialHandoffCommand)] = command=>new List<ValidationError>().ValidateRiskFinancialHandoff((AdvanceRiskFinancialHandoffCommand)command),
            [typeof(PrepareRiskManagementCommand)] = command =>
            {
                var typed=(PrepareRiskManagementCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId,typed.CommandName)
                    .ValidateEntityId(typed.EntityId,typed.CommandName).CaptureCommandValidation(()=>
                    {
                        if(typed.WorkflowId.Value==Guid.Empty || typed.InputWorkflowRevision<1)
                            throw new ArgumentException("Exact Risk preparation identity is required.");
                    });
            },
            [typeof(AcceptOrderCompositionPreparationCommand)] = command =>
            {
                var typed = (AcceptOrderCompositionPreparationCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName).CaptureCommandValidation(() =>
                    {
                        if (typed.WorkflowId.Value == Guid.Empty || typed.InputWorkflowRevision < 1 || typed.Evidence is null
                            || typed.Evidence.WorkflowId != typed.WorkflowId.Value || typed.Evidence.PreparationRevision != typed.InputWorkflowRevision)
                            throw new ArgumentException("Exact preparation identity is required.");
                    });
            },
            [typeof(ExecuteIntrinsicTimeStrategyWorkflowCommand)] = command =>
            {
                var typed = (ExecuteIntrinsicTimeStrategyWorkflowCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(CompleteRegimeDiscoveryCommand)] = command =>
            {
                var typed = (CompleteRegimeDiscoveryCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(CompleteMarketConditionCommand)] = command =>
            {
                var typed = (CompleteMarketConditionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(RedispatchCurrentStrategyPipelineCommand)] = command =>
            {
                var c=(RedispatchCurrentStrategyPipelineCommand)command;
                return new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).ValidateEntityId(c.EntityId,c.CommandName)
                    .CaptureCommandValidation(()=> { if(c.WorkflowId.Value==Guid.Empty || c.ExpectedWorkflowRevision<=0 || !Enum.IsDefined(c.ExpectedStage) || c.RequestedAtUtc.Kind!=DateTimeKind.Utc || string.IsNullOrWhiteSpace(c.RequestedBy)) throw new ArgumentException("Invalid recovery request."); });
            },
            [typeof(CompleteTradeSelectionReservationCommand)] = command =>
            {
                var typed=(CompleteTradeSelectionReservationCommand)command;
                return new List<ValidationError>().ValidateCommandId(typed.CommandId,typed.CommandName).ValidateEntityId(typed.EntityId,typed.CommandName).CaptureCommandValidation(()=>ValidateCommand(typed));
            },
            [typeof(CompleteTradeSelectionCommand)] = command =>
            {
                var typed = (CompleteTradeSelectionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(CompleteOrderCompositionCommand)] = command =>
            {
                var typed = (CompleteOrderCompositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(CompleteRiskManagementCommand)] = command =>
            {
                var typed = (CompleteRiskManagementCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(FailRegimeDiscoveryCommand)] = command =>
            {
                var typed = (FailRegimeDiscoveryCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(FailMarketConditionCommand)] = command =>
            {
                var typed = (FailMarketConditionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(FailTradeSelectionCommand)] = command =>
            {
                var typed = (FailTradeSelectionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(FailOrderCompositionCommand)] = command =>
            {
                var typed = (FailOrderCompositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(FailRiskManagementCommand)] = command =>
            {
                var typed = (FailRiskManagementCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(TimeoutMarketConditionCommand)] = command =>
            {
                var typed = (TimeoutMarketConditionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(TimeoutTradeSelectionCommand)] = command =>
            {
                var typed = (TimeoutTradeSelectionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(TimeoutOrderCompositionCommand)] = command =>
            {
                var typed = (TimeoutOrderCompositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(TimeoutRiskManagementCommand)] = command =>
            {
                var typed = (TimeoutRiskManagementCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            },
            [typeof(CancelIntrinsicTimeStrategyWorkflowCommand)] = command =>
            {
                var typed = (CancelIntrinsicTimeStrategyWorkflowCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .CaptureCommandValidation(() => ValidateCommand(typed));
            }
        };


    static readonly IReadOnlyDictionary<Type, Func<ICommand,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>,
        IntrinsicTimeStrategyWorkflowCommandState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand,
            ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>,
            IntrinsicTimeStrategyWorkflowCommandState, ValueTask<ServiceResult<GuidResult>>>>()
        {
            [typeof(AdvanceRiskFinancialHandoffCommand)] = static (command,context,state)=>((AdvanceRiskFinancialHandoffCommand)command).ExecuteAsync(context,state),
            [typeof(PrepareRiskManagementCommand)] = static (command,context,state)=>
                ((PrepareRiskManagementCommand)command).ExecuteAsync(context,state),
            [typeof(AcceptOrderCompositionPreparationCommand)] = static (command, context, state) =>
                ((AcceptOrderCompositionPreparationCommand)command).ExecuteAsync(context, state),
            [typeof(ExecuteIntrinsicTimeStrategyWorkflowCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((ExecuteIntrinsicTimeStrategyWorkflowCommand)command).Execute(context, state)),
            [typeof(CompleteRegimeDiscoveryCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((CompleteRegimeDiscoveryCommand)command).Execute(context, state)),
            [typeof(CompleteMarketConditionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((CompleteMarketConditionCommand)command).Execute(context, state)),
            [typeof(RedispatchCurrentStrategyPipelineCommand)] = static (command,context,state)=>ValueTask.FromResult(((RedispatchCurrentStrategyPipelineCommand)command).Execute(context,state)),
            [typeof(CompleteTradeSelectionReservationCommand)] = static (command,context,state)=>ValueTask.FromResult(((CompleteTradeSelectionReservationCommand)command).Execute(context,state)),
            [typeof(CompleteTradeSelectionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((CompleteTradeSelectionCommand)command).Execute(context, state)),
            [typeof(CompleteOrderCompositionCommand)] = static (command, context, state) =>
                ((CompleteOrderCompositionCommand)command).ExecutePreparedAsync(context, state),
            [typeof(CompleteRiskManagementCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((CompleteRiskManagementCommand)command).Execute(context, state)),
            [typeof(FailRegimeDiscoveryCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((FailRegimeDiscoveryCommand)command).Execute(context, state)),
            [typeof(FailMarketConditionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((FailMarketConditionCommand)command).Execute(context, state)),
            [typeof(FailTradeSelectionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((FailTradeSelectionCommand)command).Execute(context, state)),
            [typeof(FailOrderCompositionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((FailOrderCompositionCommand)command).Execute(context, state)),
            [typeof(FailRiskManagementCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((FailRiskManagementCommand)command).Execute(context, state)),
            [typeof(TimeoutMarketConditionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((TimeoutMarketConditionCommand)command).Execute(context, state)),
            [typeof(TimeoutTradeSelectionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((TimeoutTradeSelectionCommand)command).Execute(context, state)),
            [typeof(TimeoutOrderCompositionCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((TimeoutOrderCompositionCommand)command).Execute(context, state)),
            [typeof(TimeoutRiskManagementCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((TimeoutRiskManagementCommand)command).Execute(context, state)),
            [typeof(CancelIntrinsicTimeStrategyWorkflowCommand)] = static (command, context, state) =>
                ValueTask.FromResult(((CancelIntrinsicTimeStrategyWorkflowCommand)command).Execute(context, state))
        };

    /// <summary>Gets the workflow Command actor name.</summary>
    public const string ActorName = ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor;

    IIntrinsicTimeStrategyWorkflowCommandContext ActorContext =>
        Context as IIntrinsicTimeStrategyWorkflowCommandContext
        ?? throw new InvalidOperationException(
            $"{nameof(Context)} must implement {nameof(IIntrinsicTimeStrategyWorkflowCommandContext)}.");

    /// <inheritdoc />
    protected override async ValueTask OnStartup(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context)
        => await ActorContext.EventProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context)
        => await ActorContext.EventProjector.StopAsync().ConfigureAwait(false);

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
        => await ActorContext.StateRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command)
        => await ActorContext.StateRepository.SaveStateAsync(
            context,
            (IntrinsicTimeStrategyWorkflowCommandState)state,
            command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IActorState actorState,
        ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorState);
        ArgumentNullException.ThrowIfNull(command);
        var state = (IntrinsicTimeStrategyWorkflowCommandState)actorState;
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return receive(command, context, state);
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceResult<GuidResult>(command?.ErrorCode ?? 21000, ex.Message));

    static void ValidateCommand(ICommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new ArgumentException("Workflow commands require a non-empty command identity.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Subject.EntityId))
            throw new ArgumentException("Workflow commands require an entity routing identity.", nameof(command));
        if (command is ICommand<IntrinsicTimeStrategyWorkflowEntityId> entityCommand &&
            !string.Equals(command.Subject.EntityId, entityCommand.EntityId.Format(), StringComparison.Ordinal))
            throw new ArgumentException("Workflow command subject must match its entity identity.", nameof(command));

        if (command is ExecuteIntrinsicTimeStrategyWorkflowCommand execute)
        {
            if (execute.ProposedWorkflowId.Value == Guid.Empty || execute.TriggerEventId == Guid.Empty ||
                execute.CorrelationId == Guid.Empty || execute.CausationId == Guid.Empty ||
                execute.RequestedAtUtc.Kind != DateTimeKind.Utc || execute.WorkflowDefinitionVersion <= 0 ||
                execute.TriggerEvent.EntityId != execute.EntityId.ItiSignalEntityId)
                throw new ArgumentException("Workflow start requires valid workflow, trigger, trace, time, and routing identities.", nameof(command));
        }

        var completionResult = command switch
        {
            CompleteRegimeDiscoveryCommand value => value.Result,
            CompleteMarketConditionCommand value => value.Result,
            CompleteTradeSelectionCommand value => value.Result,
            CompleteOrderCompositionCommand value => value.Result,
            CompleteRiskManagementCommand value => value.Result,
            _ => null
        };
        if (completionResult is not null)
        {
            var errors = StrategyStageResultEnvelopeValidationRules.WithMaximumPayloadBytes(command is CompleteTradeSelectionCommand ? 524288 : StrategyStageResultEnvelope.DefaultMaximumPayloadBytes).Execute(completionResult);
            if (errors.Length != 0)
                throw new ArgumentException(string.Join("; ", errors.Select(value => value.ErrorMessage)),
                    nameof(command));
        }
    }
}
