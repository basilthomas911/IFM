using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;

/// <summary>
/// Owns the authoritative event-sourced state and deterministic stage transitions for Intrinsic Time Strategy workflows.
/// </summary>
/// <remarks>
/// The actor persists decisions to PostgreSQL before the conventional projector publishes dispatch lifecycle events.
/// It never sends pipeline commands directly and it has no durable Event actor dependency.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowCommandActor(
    ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> actorContext)
    : BaseEventSourceCommandActor<IntrinsicTimeStrategyWorkflowCommandActor>(actorContext, actorContext.Logger)
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [StartIntrinsicTimeStrategyWorkflowCommand.Verb] = message => message.AsCommand<StartIntrinsicTimeStrategyWorkflowCommand>()!,
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
            [TimeoutRegimeDiscoveryCommand.Verb] = message => message.AsCommand<TimeoutRegimeDiscoveryCommand>()!,
            [TimeoutMarketConditionCommand.Verb] = message => message.AsCommand<TimeoutMarketConditionCommand>()!,
            [TimeoutTradeSelectionCommand.Verb] = message => message.AsCommand<TimeoutTradeSelectionCommand>()!,
            [TimeoutOrderCompositionCommand.Verb] = message => message.AsCommand<TimeoutOrderCompositionCommand>()!,
            [TimeoutRiskManagementCommand.Verb] = message => message.AsCommand<TimeoutRiskManagementCommand>()!,
            [CancelIntrinsicTimeStrategyWorkflowCommand.Verb] = message => message.AsCommand<CancelIntrinsicTimeStrategyWorkflowCommand>()!,
            [RedispatchCurrentStrategyPipelineCommand.Verb] = message => message.AsCommand<RedispatchCurrentStrategyPipelineCommand>()!
        };

    static readonly IReadOnlyDictionary<string, Action<ICommand>> _validationMap =
        new Dictionary<string, Action<ICommand>>(StringComparer.Ordinal)
        {
            [typeof(StartIntrinsicTimeStrategyWorkflowCommand).Name] = ValidateCommand,
            [typeof(CompleteRegimeDiscoveryCommand).Name] = ValidateCommand,
            [typeof(CompleteMarketConditionCommand).Name] = ValidateCommand,
            [typeof(CompleteTradeSelectionCommand).Name] = ValidateCommand,
            [typeof(CompleteOrderCompositionCommand).Name] = ValidateCommand,
            [typeof(CompleteRiskManagementCommand).Name] = ValidateCommand,
            [typeof(FailRegimeDiscoveryCommand).Name] = ValidateCommand,
            [typeof(FailMarketConditionCommand).Name] = ValidateCommand,
            [typeof(FailTradeSelectionCommand).Name] = ValidateCommand,
            [typeof(FailOrderCompositionCommand).Name] = ValidateCommand,
            [typeof(FailRiskManagementCommand).Name] = ValidateCommand,
            [typeof(TimeoutRegimeDiscoveryCommand).Name] = ValidateCommand,
            [typeof(TimeoutMarketConditionCommand).Name] = ValidateCommand,
            [typeof(TimeoutTradeSelectionCommand).Name] = ValidateCommand,
            [typeof(TimeoutOrderCompositionCommand).Name] = ValidateCommand,
            [typeof(TimeoutRiskManagementCommand).Name] = ValidateCommand,
            [typeof(CancelIntrinsicTimeStrategyWorkflowCommand).Name] = ValidateCommand,
            [typeof(RedispatchCurrentStrategyPipelineCommand).Name] = ValidateCommand
        };

    static readonly IReadOnlyDictionary<string, Func<ICommand, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>, IntrinsicTimeStrategyWorkflowCommandState, IntrinsicTimeStrategyWorkflowCommandActor, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<string, Func<ICommand, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>, IntrinsicTimeStrategyWorkflowCommandState, IntrinsicTimeStrategyWorkflowCommandActor, ValueTask<ServiceResult<GuidResult>>>>(StringComparer.Ordinal)
        {
            [typeof(StartIntrinsicTimeStrategyWorkflowCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((StartIntrinsicTimeStrategyWorkflowCommand)command).Execute(state, static (s, c) => HandleStart(s, c))),
            [typeof(CompleteRegimeDiscoveryCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((CompleteRegimeDiscoveryCommand)command).Execute(state, static (s, c) => HandleCompletionCommand(s, c))),
            [typeof(CompleteMarketConditionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((CompleteMarketConditionCommand)command).Execute(state, static (s, c) => HandleCompletionCommand(s, c))),
            [typeof(CompleteTradeSelectionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((CompleteTradeSelectionCommand)command).Execute(state, static (s, c) => HandleCompletionCommand(s, c))),
            [typeof(CompleteOrderCompositionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((CompleteOrderCompositionCommand)command).Execute(state, static (s, c) => HandleCompletionCommand(s, c))),
            [typeof(CompleteRiskManagementCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((CompleteRiskManagementCommand)command).Execute(state, static (s, c) => HandleCompletionCommand(s, c))),
            [typeof(FailRegimeDiscoveryCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((FailRegimeDiscoveryCommand)command).Execute(state, static (s, c) => HandleFailureCommand(s, c))),
            [typeof(FailMarketConditionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((FailMarketConditionCommand)command).Execute(state, static (s, c) => HandleFailureCommand(s, c))),
            [typeof(FailTradeSelectionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((FailTradeSelectionCommand)command).Execute(state, static (s, c) => HandleFailureCommand(s, c))),
            [typeof(FailOrderCompositionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((FailOrderCompositionCommand)command).Execute(state, static (s, c) => HandleFailureCommand(s, c))),
            [typeof(FailRiskManagementCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((FailRiskManagementCommand)command).Execute(state, static (s, c) => HandleFailureCommand(s, c))),
            [typeof(TimeoutRegimeDiscoveryCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((TimeoutRegimeDiscoveryCommand)command).Execute(state, static (s, c) => HandleTimeoutCommand(s, c))),
            [typeof(TimeoutMarketConditionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((TimeoutMarketConditionCommand)command).Execute(state, static (s, c) => HandleTimeoutCommand(s, c))),
            [typeof(TimeoutTradeSelectionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((TimeoutTradeSelectionCommand)command).Execute(state, static (s, c) => HandleTimeoutCommand(s, c))),
            [typeof(TimeoutOrderCompositionCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((TimeoutOrderCompositionCommand)command).Execute(state, static (s, c) => HandleTimeoutCommand(s, c))),
            [typeof(TimeoutRiskManagementCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((TimeoutRiskManagementCommand)command).Execute(state, static (s, c) => HandleTimeoutCommand(s, c))),
            [typeof(CancelIntrinsicTimeStrategyWorkflowCommand).Name] = (command, _, state, _) => ValueTask.FromResult(((CancelIntrinsicTimeStrategyWorkflowCommand)command).Execute(state, static (s, c) => HandleCancel(s, c))),
            [typeof(RedispatchCurrentStrategyPipelineCommand).Name] = (command, _, state, actor) => ((RedispatchCurrentStrategyPipelineCommand)command).ExecuteAsync(state, actor.HandleRedispatchAsync)
        };

    /// <summary>Gets the Command actor name used by dependency injection and actor routing.</summary>
    public const string ActorName = StartIntrinsicTimeStrategyWorkflowCommand.Actor;

    IIntrinsicTimeStrategyWorkflowCommandContext ActorContext =>
        Context as IIntrinsicTimeStrategyWorkflowCommandContext
        ?? throw new InvalidOperationException($"{nameof(Context)} must implement {nameof(IIntrinsicTimeStrategyWorkflowCommandContext)}.");

    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context)
    {
        await ActorContext.EventProjector.StartAsync(context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context)
    {
        await ActorContext.EventProjector.StopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject.ActorType != ActorType.Command ||
            !string.Equals(message.Subject.Name, ActorName, StringComparison.Ordinal) ||
            !_parseMap.TryGetValue(message.Subject.Verb, out var parse))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");

        return parse(message);
    }

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        if (!_validationMap.TryGetValue(command.GetType().Name, out var validate))
            throw new InvalidOperationException($"Unsupported workflow command: {command.GetType().Name}");
        validate(command);
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
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IActorState state,
        ICommand command)
    {
        if (!_receiveMap.TryGetValue(command.GetType().Name, out var receive))
            throw new InvalidOperationException($"Unsupported workflow command: {command.GetType().Name}");
        return await receive(command, context, (IntrinsicTimeStrategyWorkflowCommandState)state, this).ConfigureAwait(false);
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
        if (command is StartIntrinsicTimeStrategyWorkflowCommand start)
        {
            var configurationErrors = new Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSetValidationRules()
                .Execute(start.RegimeDiscoveryParameterSet);
            if (configurationErrors.Length != 0)
                throw new ArgumentException(
                    string.Join("; ", configurationErrors.Select(value => value.ErrorMessage)), nameof(command));
            var expectedHash = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterPayload
                .ComputeSha256(start.RegimeDiscoveryParameterSet);
            if (!string.Equals(expectedHash, start.RegimeDiscoveryParameterPayloadSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Workflow start parameter hash does not match its immutable payload.",
                    nameof(command));
        }
        if (!TryNormalizeCompletion(command, out var completion))
            return;

        var validationErrors = new StrategyStageResultEnvelopeValidationRules().Execute(completion.Result);
        if (validationErrors.Length != 0)
            throw new ArgumentException(
                string.Join("; ", validationErrors.Select(static error => error.ErrorMessage)),
                nameof(command));
    }

    static void HandleCompletionCommand(IntrinsicTimeStrategyWorkflowCommandState state, ICommand command)
    {
        if (!TryNormalizeCompletion(command, out var completion))
            throw new InvalidOperationException($"Unsupported completion command: {command.GetType().Name}");
        HandleCompletion(state, command, completion);
    }

    static void HandleFailureCommand(IntrinsicTimeStrategyWorkflowCommandState state, ICommand command)
    {
        if (!TryNormalizeFailure(command, out var failure))
            throw new InvalidOperationException($"Unsupported failure command: {command.GetType().Name}");
        HandleFailure(state, command, failure);
    }

    static void HandleTimeoutCommand(IntrinsicTimeStrategyWorkflowCommandState state, ICommand command)
    {
        if (!TryNormalizeTimeout(command, out var timeout))
            throw new InvalidOperationException($"Unsupported timeout command: {command.GetType().Name}");
        HandleTimeout(state, command, timeout);
    }

    internal static void HandleStart(
        IntrinsicTimeStrategyWorkflowCommandState state,
        StartIntrinsicTimeStrategyWorkflowCommand command)
    {
        if (state.IsDuplicateTrigger(command.TriggerEventId))
            return;

        if (state.HasActiveWorkflow)
        {
            var active = state.ActiveWorkflow!;
            state.Update(CreateEvent<StrategyWorkflowStartRejectedEvent>(command,
                (nameof(StrategyWorkflowStartRejectedEvent.RequestedWorkflowId), command.ProposedWorkflowId),
                (nameof(StrategyWorkflowStartRejectedEvent.ActiveWorkflowId), active.WorkflowId),
                (nameof(StrategyWorkflowStartRejectedEvent.ActiveWorkflowRevision), active.WorkflowRevision),
                (nameof(StrategyWorkflowStartRejectedEvent.CorrelationId), command.CorrelationId),
                (nameof(StrategyWorkflowStartRejectedEvent.CausationId), command.CausationId),
                (nameof(StrategyWorkflowStartRejectedEvent.ActiveStage), active.CurrentStage),
                (nameof(StrategyWorkflowStartRejectedEvent.TriggerEventId), command.TriggerEventId),
                (nameof(StrategyWorkflowStartRejectedEvent.ReasonCode), "ActiveWorkflowExists"),
                (nameof(StrategyWorkflowStartRejectedEvent.RejectedAtUtc), command.RequestedAtUtc)), command);
            return;
        }

        const StrategyWorkflowStage firstStage = StrategyWorkflowStage.RegimeDiscovery;
        state.Update(CreateEvent<StrategyWorkflowStartAcceptedEvent>(command,
            (nameof(StrategyWorkflowStartAcceptedEvent.WorkflowId), command.ProposedWorkflowId),
            (nameof(StrategyWorkflowStartAcceptedEvent.WorkflowRevision), 1L),
            (nameof(StrategyWorkflowStartAcceptedEvent.CorrelationId), command.CorrelationId),
            (nameof(StrategyWorkflowStartAcceptedEvent.CausationId), command.CausationId),
            (nameof(StrategyWorkflowStartAcceptedEvent.Stage), firstStage),
            (nameof(StrategyWorkflowStartAcceptedEvent.TriggerEventId), command.TriggerEventId),
            (nameof(StrategyWorkflowStartAcceptedEvent.TriggerEvent), command.TriggerEvent),
            (nameof(StrategyWorkflowStartAcceptedEvent.WorkflowDefinitionVersion), command.WorkflowDefinitionVersion),
            (nameof(StrategyWorkflowStartAcceptedEvent.RegimeDiscoveryParameterSet), command.RegimeDiscoveryParameterSet),
            (nameof(StrategyWorkflowStartAcceptedEvent.RegimeDiscoveryParameterPayloadSha256), command.RegimeDiscoveryParameterPayloadSha256),
            (nameof(StrategyWorkflowStartAcceptedEvent.StartedAtUtc), command.RequestedAtUtc)), command);

        var route = IntrinsicTimeStrategyPipelineRoutes.Get(firstStage);
        state.Update(CreateEvent<IntrinsicTimeStrategyWorkflowStartedEvent>(command,
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.WorkflowId), command.ProposedWorkflowId),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.WorkflowRevision), 1L),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.CorrelationId), command.CorrelationId),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.CausationId), command.CausationId),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.NextPipelineStage), firstStage),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.NextPipelineActorType), route.CommandActor.ActorType),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.NextPipelineActorName), route.CommandActor.Name),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.NextPipelineBoundedContext), route.BoundedContext),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.NextPipelineCommandId), DeterministicCommandId(command.ProposedWorkflowId, firstStage, 1)),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.WorkflowState), state.ActiveWorkflow!),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.TriggerEvent), command.TriggerEvent),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.RequestedAtUtc), command.RequestedAtUtc),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.ExpectedCompletionAtUtc), (object?)null),
            (nameof(IntrinsicTimeStrategyWorkflowStartedEvent.StartedAtUtc), command.RequestedAtUtc)), command);
    }

    static void HandleCompletion(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        CompletionInput input)
    {
        if (state.HasProcessedPipelineEvent(input.SourceEventId))
        {
            var currentWorkflow = state.ActiveWorkflow;
            if (currentWorkflow is not null && state.IsConflictingPipelineResult(input.SourceEventId, input.Stage, input.Result))
                Stop(state, command, currentWorkflow.WorkflowId, currentWorkflow.WorkflowRevision + 1, currentWorkflow.CurrentStage,
                    currentWorkflow.CorrelationId, input.SourceEventId, StrategyWorkflowOutcome.ConsistencyFault,
                    "ConflictingPipelineResult", input.OccurredAtUtc);
            return;
        }
        if (!CanApplyStageInput(state, input.WorkflowId, input.Revision, input.Stage))
            return;

        var nextRevision = input.Revision + 1;
        state.Update(CreateResultEvent(command, input, nextRevision), command);
        state.Update(CreateContinuationEvent(command, input, nextRevision), command);

        var active = state.ActiveWorkflow!;
        if (input.Stage == StrategyWorkflowStage.RiskManagement)
        {
            state.Update(CreateEvent<IntrinsicTimeStrategyWorkflowCompletedEvent>(command,
                (nameof(IntrinsicTimeStrategyWorkflowCompletedEvent.WorkflowId), input.WorkflowId),
                (nameof(IntrinsicTimeStrategyWorkflowCompletedEvent.WorkflowRevision), nextRevision),
                (nameof(IntrinsicTimeStrategyWorkflowCompletedEvent.CorrelationId), input.CorrelationId),
                (nameof(IntrinsicTimeStrategyWorkflowCompletedEvent.CausationId), input.CausationId),
                (nameof(IntrinsicTimeStrategyWorkflowCompletedEvent.Stage), input.Stage),
                (nameof(IntrinsicTimeStrategyWorkflowCompletedEvent.CompletedAtUtc), input.OccurredAtUtc)), command);
            return;
        }

        var nextStage = NextStage(input.Stage);
        var route = IntrinsicTimeStrategyPipelineRoutes.Get(nextStage);
        state.Update(CreateEvent<IntrinsicTimeStrategyWorkflowContinuedEvent>(command,
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.WorkflowId), input.WorkflowId),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.WorkflowRevision), nextRevision),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.CorrelationId), input.CorrelationId),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.CausationId), input.CausationId),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.CompletedPipelineStage), input.Stage),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.NextPipelineStage), nextStage),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.NextPipelineActorType), route.CommandActor.ActorType),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.NextPipelineActorName), route.CommandActor.Name),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.NextPipelineBoundedContext), route.BoundedContext),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.NextPipelineCommandId), DeterministicCommandId(input.WorkflowId, nextStage, nextRevision)),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.WorkflowState), active),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.TriggerEvent), state.ActiveTriggerEvent!),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.ContinuationRuleSetId), "IntrinsicTimeStrategyWorkflow.v1"),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.ContinuationRuleSetVersion), 1),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.ContinuationReasonCodes), Array.Empty<string>()),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.RequestedAtUtc), input.OccurredAtUtc),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.ExpectedCompletionAtUtc), (object?)null),
            (nameof(IntrinsicTimeStrategyWorkflowContinuedEvent.ContinuedAtUtc), input.OccurredAtUtc)), command);
    }

    static void HandleFailure(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        FailureInput input)
    {
        if (!CanApplyStageInput(state, input.WorkflowId, input.Revision, input.Stage) ||
            state.HasProcessedPipelineEvent(input.SourceEventId))
            return;
        var revision = input.Revision + 1;
        state.Update(CreateFailureEvent(command, input, revision), command);
        Stop(state, command, input.WorkflowId, revision, input.Stage, input.CorrelationId,
            input.CausationId, StrategyWorkflowOutcome.PipelineFailed,
            input.Failure.ErrorCode.ToString(System.Globalization.CultureInfo.InvariantCulture), input.OccurredAtUtc);
    }

    static void HandleTimeout(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        TimeoutInput input)
    {
        if (!CanApplyStageInput(state, input.WorkflowId, input.Revision, input.Stage) ||
            state.HasProcessedTimeout(input.TimeoutId))
            return;
        var revision = input.Revision + 1;
        var correlationId = state.ActiveWorkflow!.CorrelationId;
        state.Update(CreateTimeoutEvent(command, input, revision, correlationId), command);
        Stop(state, command, input.WorkflowId, revision, input.Stage, correlationId,
            input.TimeoutId, StrategyWorkflowOutcome.TimedOut, "PipelineTimedOut", input.OccurredAtUtc);
    }

    internal static void HandleCancel(
        IntrinsicTimeStrategyWorkflowCommandState state,
        CancelIntrinsicTimeStrategyWorkflowCommand command)
    {
        var active = state.ActiveWorkflow;
        if (active is null || active.WorkflowId != command.WorkflowId ||
            active.WorkflowRevision != command.ExpectedWorkflowRevision)
            return;
        Stop(state, command, active.WorkflowId, active.WorkflowRevision + 1, active.CurrentStage,
            active.CorrelationId, command.CommandId, StrategyWorkflowOutcome.Cancelled,
            command.ReasonCode, command.RequestedAtUtc);
    }

    internal async ValueTask HandleRedispatchAsync(
        IntrinsicTimeStrategyWorkflowCommandState state,
        RedispatchCurrentStrategyPipelineCommand command)
    {
        var active = state.ActiveWorkflow;
        if (active is null || active.WorkflowId != command.WorkflowId ||
            active.WorkflowRevision != command.ExpectedWorkflowRevision ||
            active.CurrentStage != command.ExpectedStage)
            return;

        if (ActorContext.EventProjector is not EventProjector.IntrinsicTimeStrategyWorkflowEventProjector projector)
            throw new InvalidOperationException(
                $"Recovery redispatch requires {nameof(EventProjector.IntrinsicTimeStrategyWorkflowEventProjector)}.");

        await projector.RepublishCommittedDispatchAsync(state, command).ConfigureAwait(false);
    }

    static void Stop(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        StrategyWorkflowId workflowId,
        long revision,
        StrategyWorkflowStage stage,
        Guid correlationId,
        Guid causationId,
        StrategyWorkflowOutcome outcome,
        string reason,
        DateTime stoppedAtUtc)
        => state.Update(CreateEvent<IntrinsicTimeStrategyWorkflowStoppedEvent>(command,
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.WorkflowId), workflowId),
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.WorkflowRevision), revision),
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.CorrelationId), correlationId),
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.CausationId), causationId),
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.Stage), stage),
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.Outcome), outcome),
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.ReasonCode), reason ?? string.Empty),
            (nameof(IntrinsicTimeStrategyWorkflowStoppedEvent.StoppedAtUtc), stoppedAtUtc)), command);

    static bool CanApplyStageInput(
        IntrinsicTimeStrategyWorkflowCommandState state,
        StrategyWorkflowId workflowId,
        long revision,
        StrategyWorkflowStage stage)
        => state.ActiveWorkflow is { } active && active.WorkflowId == workflowId &&
           active.WorkflowRevision == revision && active.CurrentStage == stage;

    static StrategyWorkflowStage NextStage(StrategyWorkflowStage stage) => stage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => StrategyWorkflowStage.MarketCondition,
        StrategyWorkflowStage.MarketCondition => StrategyWorkflowStage.TradeSelection,
        StrategyWorkflowStage.TradeSelection => StrategyWorkflowStage.OrderComposition,
        StrategyWorkflowStage.OrderComposition => StrategyWorkflowStage.RiskManagement,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "The final stage has no successor.")
    };

    static Guid DeterministicCommandId(StrategyWorkflowId workflowId, StrategyWorkflowStage stage, long revision)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{workflowId}|{stage}|{revision}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    static TEvent CreateEvent<TEvent>(ICommand command, params (string Name, object? Value)[] values)
        where TEvent : IEvent, new()
    {
        var domainEvent = new TEvent();
        var actor = (string?)typeof(TEvent).GetField("Actor")?.GetRawConstantValue()
            ?? "IntrinsicTimeStrategyWorkflow";
        var verb = (string?)typeof(TEvent).GetField("Verb")?.GetRawConstantValue()
            ?? typeof(TEvent).Name;
        EventInitHelper.SetProperty(domainEvent, nameof(IEvent.Subject),
            new ActorSubject(ActorType.Event, actor, verb, command.Subject.EntityId));
        EventInitHelper.SetProperty(domainEvent, nameof(IEvent.Id), Guid.NewGuid());
        EventInitHelper.SetProperty(domainEvent, nameof(IEvent.ReceivedOn), DateTime.UtcNow);
        EventInitHelper.SetProperty(domainEvent, "EntityId",
            ((ICommand<IntrinsicTimeStrategyWorkflowEntityId>)command).EntityId);
        foreach (var (name, value) in values)
            EventInitHelper.SetProperty(domainEvent, name, value);
        return domainEvent;
    }

    static IEvent CreateResultEvent(ICommand command, CompletionInput input, long revision)
    {
        var values = CommonStageValues(input.WorkflowId, revision, input.CorrelationId, input.CausationId, input.Stage)
            .Concat([
                ("SourceEventId", (object?)input.SourceEventId),
                ("Result", input.Result),
                ("RecordedAtUtc", input.OccurredAtUtc)
            ]).ToArray();
        return input.Stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => CreateEvent<StrategyWorkflowRegimeDiscoveryResultRecordedEvent>(command, values),
            StrategyWorkflowStage.MarketCondition => CreateEvent<StrategyWorkflowMarketConditionResultRecordedEvent>(command, values),
            StrategyWorkflowStage.TradeSelection => CreateEvent<StrategyWorkflowTradeSelectionResultRecordedEvent>(command, values),
            StrategyWorkflowStage.OrderComposition => CreateEvent<StrategyWorkflowOrderCompositionResultRecordedEvent>(command, values),
            StrategyWorkflowStage.RiskManagement => CreateEvent<StrategyWorkflowRiskManagementResultRecordedEvent>(command, values),
            _ => throw new ArgumentOutOfRangeException(nameof(input.Stage))
        };
    }

    static IEvent CreateContinuationEvent(ICommand command, CompletionInput input, long revision)
    {
        var values = CommonStageValues(input.WorkflowId, revision, input.CorrelationId, input.CausationId, input.Stage)
            .Concat([
                ("Decision", (object?)StrategyWorkflowContinuationDecision.Proceed),
                ("RuleSetId", "IntrinsicTimeStrategyWorkflow.v1"),
                ("RuleSetVersion", 1),
                ("ReasonCodes", Array.Empty<string>()),
                ("EvaluatedAtUtc", input.OccurredAtUtc)
            ]).ToArray();
        return input.Stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => CreateEvent<StrategyWorkflowRegimeDiscoveryContinuationEvaluatedEvent>(command, values),
            StrategyWorkflowStage.MarketCondition => CreateEvent<StrategyWorkflowMarketConditionContinuationEvaluatedEvent>(command, values),
            StrategyWorkflowStage.TradeSelection => CreateEvent<StrategyWorkflowTradeSelectionContinuationEvaluatedEvent>(command, values),
            StrategyWorkflowStage.OrderComposition => CreateEvent<StrategyWorkflowOrderCompositionContinuationEvaluatedEvent>(command, values),
            StrategyWorkflowStage.RiskManagement => CreateEvent<StrategyWorkflowRiskManagementContinuationEvaluatedEvent>(command, values),
            _ => throw new ArgumentOutOfRangeException(nameof(input.Stage))
        };
    }

    static IEvent CreateFailureEvent(ICommand command, FailureInput input, long revision)
    {
        var values = CommonStageValues(input.WorkflowId, revision, input.CorrelationId, input.CausationId, input.Stage)
            .Concat([
                ("SourceEventId", (object?)input.SourceEventId),
                ("Failure", input.Failure),
                ("FailedAtUtc", input.OccurredAtUtc)
            ]).ToArray();
        return input.Stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => CreateEvent<StrategyWorkflowRegimeDiscoveryFailedEvent>(command, values),
            StrategyWorkflowStage.MarketCondition => CreateEvent<StrategyWorkflowMarketConditionFailedEvent>(command, values),
            StrategyWorkflowStage.TradeSelection => CreateEvent<StrategyWorkflowTradeSelectionFailedEvent>(command, values),
            StrategyWorkflowStage.OrderComposition => CreateEvent<StrategyWorkflowOrderCompositionFailedEvent>(command, values),
            StrategyWorkflowStage.RiskManagement => CreateEvent<StrategyWorkflowRiskManagementFailedEvent>(command, values),
            _ => throw new ArgumentOutOfRangeException(nameof(input.Stage))
        };
    }

    static IEvent CreateTimeoutEvent(ICommand command, TimeoutInput input, long revision, Guid correlationId)
    {
        var active = ((ICommand<IntrinsicTimeStrategyWorkflowEntityId>)command);
        var values = CommonStageValues(input.WorkflowId, revision, correlationId, input.TimeoutId, input.Stage)
            .Concat([
                ("TimeoutId", (object?)input.TimeoutId),
                ("TimedOutAtUtc", input.OccurredAtUtc)
            ]).ToArray();
        return input.Stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => CreateEvent<StrategyWorkflowRegimeDiscoveryTimedOutEvent>(active, values),
            StrategyWorkflowStage.MarketCondition => CreateEvent<StrategyWorkflowMarketConditionTimedOutEvent>(active, values),
            StrategyWorkflowStage.TradeSelection => CreateEvent<StrategyWorkflowTradeSelectionTimedOutEvent>(active, values),
            StrategyWorkflowStage.OrderComposition => CreateEvent<StrategyWorkflowOrderCompositionTimedOutEvent>(active, values),
            StrategyWorkflowStage.RiskManagement => CreateEvent<StrategyWorkflowRiskManagementTimedOutEvent>(active, values),
            _ => throw new ArgumentOutOfRangeException(nameof(input.Stage))
        };
    }

    static (string Name, object? Value)[] CommonStageValues(
        StrategyWorkflowId workflowId,
        long revision,
        Guid correlationId,
        Guid causationId,
        StrategyWorkflowStage stage)
        =>
        [
            ("WorkflowId", workflowId),
            ("WorkflowRevision", revision),
            ("CorrelationId", correlationId),
            ("CausationId", causationId),
            ("Stage", stage)
        ];

    static bool TryNormalizeCompletion(ICommand command, out CompletionInput input)
    {
        input = command switch
        {
            CompleteRegimeDiscoveryCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.RegimeDiscovery, c.SourceEventId, c.Result, c.CorrelationId, c.CausationId, c.CompletedAtUtc),
            CompleteMarketConditionCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.MarketCondition, c.SourceEventId, c.Result, c.CorrelationId, c.CausationId, c.CompletedAtUtc),
            CompleteTradeSelectionCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.TradeSelection, c.SourceEventId, c.Result, c.CorrelationId, c.CausationId, c.CompletedAtUtc),
            CompleteOrderCompositionCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.OrderComposition, c.SourceEventId, c.Result, c.CorrelationId, c.CausationId, c.CompletedAtUtc),
            CompleteRiskManagementCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.RiskManagement, c.SourceEventId, c.Result, c.CorrelationId, c.CausationId, c.CompletedAtUtc),
            _ => default
        };
        return input.Result is not null;
    }

    static bool TryNormalizeFailure(ICommand command, out FailureInput input)
    {
        input = command switch
        {
            FailRegimeDiscoveryCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.RegimeDiscovery, c.SourceEventId, c.Failure, c.CorrelationId, c.CausationId, c.FailedAtUtc),
            FailMarketConditionCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.MarketCondition, c.SourceEventId, c.Failure, c.CorrelationId, c.CausationId, c.FailedAtUtc),
            FailTradeSelectionCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.TradeSelection, c.SourceEventId, c.Failure, c.CorrelationId, c.CausationId, c.FailedAtUtc),
            FailOrderCompositionCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.OrderComposition, c.SourceEventId, c.Failure, c.CorrelationId, c.CausationId, c.FailedAtUtc),
            FailRiskManagementCommand c => new(c.WorkflowId, c.InputWorkflowRevision, StrategyWorkflowStage.RiskManagement, c.SourceEventId, c.Failure, c.CorrelationId, c.CausationId, c.FailedAtUtc),
            _ => default
        };
        return input.Failure is not null;
    }

    static bool TryNormalizeTimeout(ICommand command, out TimeoutInput input)
    {
        input = command switch
        {
            TimeoutRegimeDiscoveryCommand c => new(c.WorkflowId, c.ExpectedWorkflowRevision, StrategyWorkflowStage.RegimeDiscovery, c.TimeoutId, c.TimedOutAtUtc),
            TimeoutMarketConditionCommand c => new(c.WorkflowId, c.ExpectedWorkflowRevision, StrategyWorkflowStage.MarketCondition, c.TimeoutId, c.TimedOutAtUtc),
            TimeoutTradeSelectionCommand c => new(c.WorkflowId, c.ExpectedWorkflowRevision, StrategyWorkflowStage.TradeSelection, c.TimeoutId, c.TimedOutAtUtc),
            TimeoutOrderCompositionCommand c => new(c.WorkflowId, c.ExpectedWorkflowRevision, StrategyWorkflowStage.OrderComposition, c.TimeoutId, c.TimedOutAtUtc),
            TimeoutRiskManagementCommand c => new(c.WorkflowId, c.ExpectedWorkflowRevision, StrategyWorkflowStage.RiskManagement, c.TimeoutId, c.TimedOutAtUtc),
            _ => default
        };
        return input.TimeoutId != Guid.Empty;
    }

    readonly record struct CompletionInput(
        StrategyWorkflowId WorkflowId, long Revision, StrategyWorkflowStage Stage, Guid SourceEventId,
        StrategyStageResultEnvelope Result, Guid CorrelationId, Guid CausationId, DateTime OccurredAtUtc);

    readonly record struct FailureInput(
        StrategyWorkflowId WorkflowId, long Revision, StrategyWorkflowStage Stage, Guid SourceEventId,
        StrategyPipelineFailure Failure, Guid CorrelationId, Guid CausationId, DateTime OccurredAtUtc);

    readonly record struct TimeoutInput(
        StrategyWorkflowId WorkflowId, long Revision, StrategyWorkflowStage Stage, Guid TimeoutId, DateTime OccurredAtUtc);
}
