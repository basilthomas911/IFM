using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
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


    static readonly IReadOnlyDictionary<Type, Func<IntrinsicTimeStrategyWorkflowCommandActor, ICommand,
        IntrinsicTimeStrategyWorkflowCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<IntrinsicTimeStrategyWorkflowCommandActor, ICommand,
            IntrinsicTimeStrategyWorkflowCommandState, ServiceResult<GuidResult>>>()
        {
            [typeof(ExecuteIntrinsicTimeStrategyWorkflowCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (ExecuteIntrinsicTimeStrategyWorkflowCommand)command),
            [typeof(CompleteRegimeDiscoveryCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (CompleteRegimeDiscoveryCommand)command),
            [typeof(CompleteMarketConditionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (CompleteMarketConditionCommand)command),
            [typeof(CompleteTradeSelectionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (CompleteTradeSelectionCommand)command),
            [typeof(CompleteOrderCompositionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (CompleteOrderCompositionCommand)command),
            [typeof(CompleteRiskManagementCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (CompleteRiskManagementCommand)command),
            [typeof(FailRegimeDiscoveryCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (FailRegimeDiscoveryCommand)command),
            [typeof(FailMarketConditionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (FailMarketConditionCommand)command),
            [typeof(FailTradeSelectionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (FailTradeSelectionCommand)command),
            [typeof(FailOrderCompositionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (FailOrderCompositionCommand)command),
            [typeof(FailRiskManagementCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (FailRiskManagementCommand)command),
            [typeof(TimeoutMarketConditionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (TimeoutMarketConditionCommand)command),
            [typeof(TimeoutTradeSelectionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (TimeoutTradeSelectionCommand)command),
            [typeof(TimeoutOrderCompositionCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (TimeoutOrderCompositionCommand)command),
            [typeof(TimeoutRiskManagementCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (TimeoutRiskManagementCommand)command),
            [typeof(CancelIntrinsicTimeStrategyWorkflowCommand)] = static (actor, command, state) =>
                actor.ProcessWorkflowCommand(state, (CancelIntrinsicTimeStrategyWorkflowCommand)command)
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
        var state = (IntrinsicTimeStrategyWorkflowCommandState)actorState;
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receive(this, command, state));
    }

    ServiceResult<GuidResult> ProcessWorkflowCommand(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command)
    {
        var before = state.CurrentView;
        var eventCount = state.Events.Count;
        switch (command)
        {
            case ExecuteIntrinsicTimeStrategyWorkflowCommand execute:
                HandleExecute(state, execute, ActorContext.TimeProvider,
                    ActorContext.ExecutionOptions.MaximumExecutionDuration);
                break;
            case CancelIntrinsicTimeStrategyWorkflowCommand cancel:
                HandleCancel(state, cancel, ActorContext.TimeProvider);
                break;
            default:
                if (TryNormalizeCompletion(command, out var completion))
                    HandleCompletion(state, command, completion, ActorContext.TimeProvider);
                else if (TryNormalizeFailure(command, out var failure))
                    HandleFailure(state, command, failure, ActorContext.TimeProvider);
                else if (TryNormalizeTimeout(command, out var timeout))
                    HandleTimeout(state, command, timeout, ActorContext.TimeProvider);
                else
                    throw new InvalidOperationException($"Unsupported workflow command: {command.GetType().Name}");
                break;
        }
        LogTransitionObservation(command, before, state.CurrentView, state.Events.Count - eventCount);
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }

    void LogTransitionObservation(
        ICommand command,
        IntrinsicTimeStrategyWorkflowView? before,
        IntrinsicTimeStrategyWorkflowView? after,
        int appendedEvents)
    {
        if (command is ExecuteIntrinsicTimeStrategyWorkflowCommand execute)
        {
            if (appendedEvents == 0 && before?.TriggerEventId != execute.TriggerEventId)
                ActorContext.Logger.LogWarning(
                    "Workflow Execute rejected as busy for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                    execute.EntityId.Format(), before?.WorkflowId, before?.WorkflowRevision);
            else if (appendedEvents == 2)
                ActorContext.Logger.LogWarning(
                    "Expired workflow {ExpiredWorkflowId} was lazily closed and replaced by {WorkflowId} for {WorkflowEntityId}",
                    before?.WorkflowId, after?.WorkflowId, execute.EntityId.Format());
            return;
        }

        if (appendedEvents == 0)
        {
            ActorContext.Logger.LogWarning(
                "Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.CommandName, command.Subject.EntityId, before?.WorkflowId, before?.WorkflowRevision);
            return;
        }

        if (before is { Status: WorkflowStrategyMachineStatus.Started } &&
            after is { Status: WorkflowStrategyMachineStatus.TimedOut })
            ActorContext.Logger.LogWarning(
                "Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.Subject.EntityId, after.WorkflowId, after.WorkflowRevision);
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
        }

        if (TryNormalizeCompletion(command, out var completion))
        {
            var errors = new StrategyStageResultEnvelopeValidationRules().Execute(completion.Result);
            if (errors.Length != 0)
                throw new ArgumentException(string.Join("; ", errors.Select(value => value.ErrorMessage)),
                    nameof(command));
        }
    }

    internal static void HandleExecute(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ExecuteIntrinsicTimeStrategyWorkflowCommand command,
        TimeProvider timeProvider,
        TimeSpan maximumExecutionDuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (maximumExecutionDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumExecutionDuration));

        var now = UtcNow(timeProvider);
        var current = state.CurrentView;
        if (current?.TriggerEventId == command.TriggerEventId)
            return;
        if (current is { Status: WorkflowStrategyMachineStatus.Started } && now < current.ExpiresAtUtc)
            return;
        if (current is { Status: WorkflowStrategyMachineStatus.Started })
        {
            var expired = TerminalView(
                current,
                WorkflowStrategyMachineStatus.TimedOut,
                StrategyActorProcessingStatus.TimedOut,
                current.WorkflowRevision + 1,
                command.CommandId,
                now,
                "WorkflowExecutionExpired",
                CreateTimeoutFailure(now));
            AppendSnapshot(state, command, current.Status, expired, now);
            current = expired;
        }

        var expiresAtUtc = now.Add(maximumExecutionDuration);
        var parameterSet = command.RegimeDiscoveryParameterSet;
        var started = new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = command.EntityId,
            WorkflowId = command.ProposedWorkflowId,
            TriggerEventId = command.TriggerEventId,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            WorkflowDefinitionVersion = command.WorkflowDefinitionVersion,
            Status = WorkflowStrategyMachineStatus.Started,
            CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            WorkflowRevision = 1,
            StartedAtUtc = now,
            UpdatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            RegimeDiscovery = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = now,
                InputWorkflowRevision = 1,
                ParameterSetId = parameterSet.ParameterSetId,
                ParameterSetVersion = parameterSet.Version,
                ParameterPayloadSha256 = command.RegimeDiscoveryParameterPayloadSha256,
                ExpiresAtUtc = expiresAtUtc
            },
            RegimeDiscoveryParameterSet = parameterSet,
            RegimeDiscoveryParameterPayloadSha256 = command.RegimeDiscoveryParameterPayloadSha256,
            TriggerEvent = command.TriggerEvent
        };
        AppendSnapshot(state, command, current?.Status ?? WorkflowStrategyMachineStatus.Empty, started, now);
    }

    internal static void HandleCompletionForTest(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        TimeProvider timeProvider)
    {
        if (!TryNormalizeCompletion(command, out var input))
            throw new ArgumentException("A completion command is required.", nameof(command));
        HandleCompletion(state, command, input, timeProvider);
    }

    static void HandleCompletion(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        CompletionInput input,
        TimeProvider timeProvider)
    {
        var current = state.CurrentView;
        if (!MatchesCurrent(current, input.WorkflowId, input.Revision, input.Stage))
            return;
        var stage = GetStage(current!, input.Stage);
        if (stage.SourceEventId == input.SourceEventId)
            return;

        var now = UtcNow(timeProvider);
        if (now >= current!.ExpiresAtUtc)
        {
            var timedOut = TerminalView(current, WorkflowStrategyMachineStatus.TimedOut,
                StrategyActorProcessingStatus.TimedOut, current.WorkflowRevision + 1, input.SourceEventId, now,
                "WorkflowExecutionExpired", CreateTimeoutFailure(now), input.SourceEventId);
            AppendSnapshot(state, command, current.Status, timedOut, now);
            return;
        }

        var revision = current.WorkflowRevision + 1;
        var completedStage = stage with
        {
            ProcessingStatus = StrategyActorProcessingStatus.Completed,
            ContinuationDecision = StrategyWorkflowContinuationDecision.Proceed,
            CompletedAtUtc = now,
            FailedAtUtc = null,
            Result = input.Result,
            Failure = null,
            SourceEventId = input.SourceEventId,
            ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.v1",
            ContinuationRuleSetVersion = 1,
            ContinuationReasonCodes = []
        };
        var updated = SetStage(current with
        {
            CausationId = input.CausationId,
            WorkflowRevision = revision,
            UpdatedAtUtc = now
        }, input.Stage, completedStage);

        if (input.Stage == StrategyWorkflowStage.RiskManagement)
            updated = updated with { Status = WorkflowStrategyMachineStatus.Completed, TerminalAtUtc = now };
        else
        {
            var next = NextStage(input.Stage);
            updated = SetStage(updated with { CurrentStage = next }, next, new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = now,
                InputWorkflowRevision = revision,
                ExpiresAtUtc = current.ExpiresAtUtc
            });
        }
        AppendSnapshot(state, command, current.Status, updated, now);
    }

    internal static void HandleFailureForTest(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        TimeProvider timeProvider)
    {
        if (!TryNormalizeFailure(command, out var input))
            throw new ArgumentException("A failure command is required.", nameof(command));
        HandleFailure(state, command, input, timeProvider);
    }

    static void HandleFailure(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        FailureInput input,
        TimeProvider timeProvider)
    {
        var current = state.CurrentView;
        if (!MatchesCurrent(current, input.WorkflowId, input.Revision, input.Stage))
            return;
        if (GetStage(current!, input.Stage).SourceEventId == input.SourceEventId)
            return;

        var now = UtcNow(timeProvider);
        var timedOut = now >= current!.ExpiresAtUtc || IsTimeoutFailure(input.Failure);
        var updated = TerminalView(
            current,
            timedOut ? WorkflowStrategyMachineStatus.TimedOut : WorkflowStrategyMachineStatus.Failed,
            timedOut ? StrategyActorProcessingStatus.TimedOut : StrategyActorProcessingStatus.Failed,
            current.WorkflowRevision + 1,
            input.CausationId,
            now,
            timedOut ? "PipelineTimedOut" : input.Failure.ErrorCode.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            input.Failure,
            input.SourceEventId);
        AppendSnapshot(state, command, current.Status, updated, now);
    }

    static void HandleTimeout(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        TimeoutInput input,
        TimeProvider timeProvider)
    {
        var current = state.CurrentView;
        if (!MatchesCurrent(current, input.WorkflowId, input.Revision, input.Stage))
            return;
        var now = UtcNow(timeProvider);
        var updated = TerminalView(current!, WorkflowStrategyMachineStatus.TimedOut,
            StrategyActorProcessingStatus.TimedOut, current!.WorkflowRevision + 1, input.TimeoutId, now,
            "PipelineTimedOut", CreateTimeoutFailure(now), input.TimeoutId);
        AppendSnapshot(state, command, current.Status, updated, now);
    }

    internal static void HandleCancel(
        IntrinsicTimeStrategyWorkflowCommandState state,
        CancelIntrinsicTimeStrategyWorkflowCommand command,
        TimeProvider timeProvider)
    {
        var current = state.CurrentView;
        if (!MatchesCurrent(current, command.WorkflowId, command.ExpectedWorkflowRevision,
                current?.CurrentStage ?? StrategyWorkflowStage.None))
            return;
        var now = UtcNow(timeProvider);
        var updated = TerminalView(current!, WorkflowStrategyMachineStatus.Cancelled,
            StrategyActorProcessingStatus.Cancelled, current!.WorkflowRevision + 1, command.CommandId, now,
            command.ReasonCode, new StrategyPipelineFailure
            {
                ErrorMessage = command.ReasonCode,
                ErrorType = "Cancelled",
                FailedAtUtc = now
            });
        AppendSnapshot(state, command, current.Status, updated, now);
    }

    static IntrinsicTimeStrategyWorkflowView TerminalView(
        IntrinsicTimeStrategyWorkflowView current,
        WorkflowStrategyMachineStatus status,
        StrategyActorProcessingStatus stageStatus,
        long revision,
        Guid causationId,
        DateTime now,
        string reason,
        StrategyPipelineFailure failure,
        Guid sourceEventId = default)
    {
        var stage = GetStage(current, current.CurrentStage) with
        {
            ProcessingStatus = stageStatus,
            FailedAtUtc = now,
            Failure = failure,
            SourceEventId = sourceEventId
        };
        return SetStage(current with
        {
            Status = status,
            WorkflowRevision = revision,
            CausationId = causationId,
            UpdatedAtUtc = now,
            TerminalAtUtc = now,
            StopReasonCode = reason ?? string.Empty
        }, current.CurrentStage, stage);
    }

    static void AppendSnapshot(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view,
        DateTime now)
    {
        var entityCommand = (ICommand<IntrinsicTimeStrategyWorkflowEntityId>)command;
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor,
                WorkflowStrategyStateUpdatedEvent.Verb, entityCommand.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
            EntityId = entityCommand.EntityId,
            CommandId = command.CommandId,
            AggregateId = entityCommand.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = now,
            WorkflowId = view.WorkflowId,
            WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId,
            CausationId = view.CausationId,
            PreviousStatus = previousStatus,
            State = view,
            UpdatedAtUtc = now
        }, command);
    }

    internal static Guid DeterministicPipelineCommandId(
        StrategyWorkflowId workflowId,
        StrategyWorkflowStage stage,
        long revision)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{workflowId}|{stage}|{revision}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    static bool MatchesCurrent(
        IntrinsicTimeStrategyWorkflowView? current,
        StrategyWorkflowId workflowId,
        long revision,
        StrategyWorkflowStage stage)
        => current is { Status: WorkflowStrategyMachineStatus.Started } &&
           current.WorkflowId == workflowId && current.WorkflowRevision == revision && current.CurrentStage == stage;

    static bool IsTimeoutFailure(StrategyPipelineFailure failure)
        => failure.ErrorCode == 23103 ||
           failure.ErrorType.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
           failure.ErrorType.Contains("TimedOut", StringComparison.OrdinalIgnoreCase);

    static StrategyPipelineFailure CreateTimeoutFailure(DateTime now) => new()
    {
        ErrorCode = 23103,
        ErrorMessage = "The fixed workflow execution deadline was reached.",
        ErrorType = "RegimeDiscoveryTimedOut",
        FailedAtUtc = now
    };

    static StrategyWorkflowStageState GetStage(
        IntrinsicTimeStrategyWorkflowView view,
        StrategyWorkflowStage stage)
        => stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => view.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => view.MarketCondition,
            StrategyWorkflowStage.TradeSelection => view.TradeSelection,
            StrategyWorkflowStage.OrderComposition => view.OrderComposition,
            StrategyWorkflowStage.RiskManagement => view.RiskManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "A concrete stage is required.")
        };

    static IntrinsicTimeStrategyWorkflowView SetStage(
        IntrinsicTimeStrategyWorkflowView view,
        StrategyWorkflowStage stage,
        StrategyWorkflowStageState value)
        => stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => view with { RegimeDiscovery = value },
            StrategyWorkflowStage.MarketCondition => view with { MarketCondition = value },
            StrategyWorkflowStage.TradeSelection => view with { TradeSelection = value },
            StrategyWorkflowStage.OrderComposition => view with { OrderComposition = value },
            StrategyWorkflowStage.RiskManagement => view with { RiskManagement = value },
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "A concrete stage is required.")
        };

    static StrategyWorkflowStage NextStage(StrategyWorkflowStage stage) => stage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => StrategyWorkflowStage.MarketCondition,
        StrategyWorkflowStage.MarketCondition => StrategyWorkflowStage.TradeSelection,
        StrategyWorkflowStage.TradeSelection => StrategyWorkflowStage.OrderComposition,
        StrategyWorkflowStage.OrderComposition => StrategyWorkflowStage.RiskManagement,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "The final stage has no successor.")
    };

    static DateTime UtcNow(TimeProvider timeProvider) => timeProvider.GetUtcNow().UtcDateTime;

    static bool TryNormalizeCompletion(ICommand command, out CompletionInput input)
    {
        input = command switch
        {
            CompleteRegimeDiscoveryCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.RegimeDiscovery, value.SourceEventId, value.Result, value.CausationId),
            CompleteMarketConditionCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.MarketCondition, value.SourceEventId, value.Result, value.CausationId),
            CompleteTradeSelectionCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.TradeSelection, value.SourceEventId, value.Result, value.CausationId),
            CompleteOrderCompositionCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.OrderComposition, value.SourceEventId, value.Result, value.CausationId),
            CompleteRiskManagementCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.RiskManagement, value.SourceEventId, value.Result, value.CausationId),
            _ => default
        };
        return input.Result is not null;
    }

    static bool TryNormalizeFailure(ICommand command, out FailureInput input)
    {
        input = command switch
        {
            FailRegimeDiscoveryCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.RegimeDiscovery, value.SourceEventId, value.Failure, value.CausationId),
            FailMarketConditionCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.MarketCondition, value.SourceEventId, value.Failure, value.CausationId),
            FailTradeSelectionCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.TradeSelection, value.SourceEventId, value.Failure, value.CausationId),
            FailOrderCompositionCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.OrderComposition, value.SourceEventId, value.Failure, value.CausationId),
            FailRiskManagementCommand value => new(value.WorkflowId, value.InputWorkflowRevision,
                StrategyWorkflowStage.RiskManagement, value.SourceEventId, value.Failure, value.CausationId),
            _ => default
        };
        return input.Failure is not null;
    }

    static bool TryNormalizeTimeout(ICommand command, out TimeoutInput input)
    {
        input = command switch
        {
            TimeoutMarketConditionCommand value => new(value.WorkflowId, value.ExpectedWorkflowRevision,
                StrategyWorkflowStage.MarketCondition, value.TimeoutId),
            TimeoutTradeSelectionCommand value => new(value.WorkflowId, value.ExpectedWorkflowRevision,
                StrategyWorkflowStage.TradeSelection, value.TimeoutId),
            TimeoutOrderCompositionCommand value => new(value.WorkflowId, value.ExpectedWorkflowRevision,
                StrategyWorkflowStage.OrderComposition, value.TimeoutId),
            TimeoutRiskManagementCommand value => new(value.WorkflowId, value.ExpectedWorkflowRevision,
                StrategyWorkflowStage.RiskManagement, value.TimeoutId),
            _ => default
        };
        return input.TimeoutId != Guid.Empty;
    }

    readonly record struct CompletionInput(
        StrategyWorkflowId WorkflowId,
        long Revision,
        StrategyWorkflowStage Stage,
        Guid SourceEventId,
        StrategyStageResultEnvelope Result,
        Guid CausationId);

    readonly record struct FailureInput(
        StrategyWorkflowId WorkflowId,
        long Revision,
        StrategyWorkflowStage Stage,
        Guid SourceEventId,
        StrategyPipelineFailure Failure,
        Guid CausationId);

    readonly record struct TimeoutInput(
        StrategyWorkflowId WorkflowId,
        long Revision,
        StrategyWorkflowStage Stage,
        Guid TimeoutId);
}
