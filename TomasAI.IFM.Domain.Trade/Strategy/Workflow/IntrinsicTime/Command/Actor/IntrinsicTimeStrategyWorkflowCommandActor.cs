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
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb] =
                message => message.AsCommand<ExecuteIntrinsicTimeStrategyWorkflowCommand>()!,
            [CompleteRegimeDiscoveryCommand.Verb] = message => message.AsCommand<CompleteRegimeDiscoveryCommand>()!,
            [CompleteMarketConditionCommand.Verb] = message => message.AsCommand<CompleteMarketConditionCommand>()!,
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
        IntrinsicTimeStrategyWorkflowCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand,
            ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>,
            IntrinsicTimeStrategyWorkflowCommandState, ServiceResult<GuidResult>>>()
        {
            [typeof(ExecuteIntrinsicTimeStrategyWorkflowCommand)] = static (command, context, state) =>
                ((ExecuteIntrinsicTimeStrategyWorkflowCommand)command).Execute(context, state),
            [typeof(CompleteRegimeDiscoveryCommand)] = static (command, context, state) =>
                ((CompleteRegimeDiscoveryCommand)command).Execute(context, state),
            [typeof(CompleteMarketConditionCommand)] = static (command, context, state) =>
                ((CompleteMarketConditionCommand)command).Execute(context, state),
            [typeof(CompleteTradeSelectionCommand)] = static (command, context, state) =>
                ((CompleteTradeSelectionCommand)command).Execute(context, state),
            [typeof(CompleteOrderCompositionCommand)] = static (command, context, state) =>
                ((CompleteOrderCompositionCommand)command).Execute(context, state),
            [typeof(CompleteRiskManagementCommand)] = static (command, context, state) =>
                ((CompleteRiskManagementCommand)command).Execute(context, state),
            [typeof(FailRegimeDiscoveryCommand)] = static (command, context, state) =>
                ((FailRegimeDiscoveryCommand)command).Execute(context, state),
            [typeof(FailMarketConditionCommand)] = static (command, context, state) =>
                ((FailMarketConditionCommand)command).Execute(context, state),
            [typeof(FailTradeSelectionCommand)] = static (command, context, state) =>
                ((FailTradeSelectionCommand)command).Execute(context, state),
            [typeof(FailOrderCompositionCommand)] = static (command, context, state) =>
                ((FailOrderCompositionCommand)command).Execute(context, state),
            [typeof(FailRiskManagementCommand)] = static (command, context, state) =>
                ((FailRiskManagementCommand)command).Execute(context, state),
            [typeof(TimeoutMarketConditionCommand)] = static (command, context, state) =>
                ((TimeoutMarketConditionCommand)command).Execute(context, state),
            [typeof(TimeoutTradeSelectionCommand)] = static (command, context, state) =>
                ((TimeoutTradeSelectionCommand)command).Execute(context, state),
            [typeof(TimeoutOrderCompositionCommand)] = static (command, context, state) =>
                ((TimeoutOrderCompositionCommand)command).Execute(context, state),
            [typeof(TimeoutRiskManagementCommand)] = static (command, context, state) =>
                ((TimeoutRiskManagementCommand)command).Execute(context, state),
            [typeof(CancelIntrinsicTimeStrategyWorkflowCommand)] = static (command, context, state) =>
                ((CancelIntrinsicTimeStrategyWorkflowCommand)command).Execute(context, state)
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
        return ValueTask.FromResult(receive(command, context, state));
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
            var errors = new RegimeDiscoveryParameterSetValidationRules().Execute(execute.RegimeDiscoveryParameterSet);
            if (errors.Length != 0)
                throw new ArgumentException(string.Join("; ", errors.Select(value => value.ErrorMessage)),
                    nameof(command));
            if (!string.Equals(
                    RegimeDiscoveryParameterPayload.ComputeSha256(execute.RegimeDiscoveryParameterSet),
                    execute.RegimeDiscoveryParameterPayloadSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Workflow start parameter hash does not match its immutable payload.",
                    nameof(command));
            var binding = execute.AssessmentBinding
                ?? throw new ArgumentException("Workflow start requires a frozen Market Condition assessment profile.", nameof(command));
            binding.Validate();
            if (execute.FundId <= 0 || binding.Parameters.TargetHorizon != execute.TriggerEvent.EntityId.TimePeriod ||
                binding.Parameters.HorizonProfile.RegimeProfileId != execute.RegimeDiscoveryParameterSet.ParameterSetId ||
                binding.Parameters.HorizonProfile.RegimeProfileVersion != execute.RegimeDiscoveryParameterSet.Version)
                throw new ArgumentException("Assessment workflow profile does not match the triggering horizon and frozen Regime Discovery configuration.");
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
            var errors = new StrategyStageResultEnvelopeValidationRules().Execute(completionResult);
            if (errors.Length != 0)
                throw new ArgumentException(string.Join("; ", errors.Select(value => value.ErrorMessage)),
                    nameof(command));
        }
    }
}
