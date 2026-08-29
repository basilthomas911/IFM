using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

/// <summary>Requests workflows from eligible ITI triggers and executes only committed Started pipeline snapshots.</summary>
/// <remarks>
/// This stateless actor has no replay, resume, or redispatch. For Regime Discovery it owns the direct Function
/// request and translates the typed terminal reply into a Strategy Workflow complete or fail command.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowRealtimeActor(
    IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> actorContext)
    : BaseEventActor<IntrinsicTimeStrategyWorkflowRealtimeActor>(actorContext, RequireContext(actorContext).Logger)
{
    static readonly TimeSpan FunctionReplyGrace = TimeSpan.FromSeconds(5);

    static readonly ActorTypeId TriggerRoute = new(
        ActorType.Realtime,
        FuturesItiSignalGeneratedEvent.RealtimeActor,
        FuturesItiSignalGeneratedEvent.Verb);

    /// <summary>Gets the workflow Realtime actor name.</summary>
    public const string ActorName = "IntrinsicTimeStrategyWorkflowRealtime";

    IIntrinsicTimeStrategyWorkflowRealtimeContext ActorContext { get; } = RequireContext(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesItiSignalGeneratedEvent.Verb] =
                message => message.AsEvent<FuturesItiSignalGeneratedEvent>()!,
            [WorkflowStrategyStateUpdatedEvent.Verb] =
                message => message.AsEvent<WorkflowStrategyStateUpdatedEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<
        IntrinsicTimeStrategyWorkflowRealtimeActor,
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>,
        IEvent,
        ValueTask>> _receiveMap =
        new Dictionary<Type, Func<
            IntrinsicTimeStrategyWorkflowRealtimeActor,
            IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>,
            IEvent,
            ValueTask>>
        {
            [typeof(FuturesItiSignalGeneratedEvent)] = static (actor, context, @event) =>
                actor.ExecuteWorkflowAsync(context, (FuturesItiSignalGeneratedEvent)@event),
            [typeof(WorkflowStrategyStateUpdatedEvent)] = static async (actor, context, @event) =>
            {
                var snapshot = (WorkflowStrategyStateUpdatedEvent)@event;
                if (snapshot.State is { Status: WorkflowStrategyMachineStatus.Started })
                    await DispatchCommittedStateAsync(context, snapshot).ConfigureAwait(false);
            }
        };

    delegate ValueTask PipelineExecutionHandler(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        WorkflowStrategyStateUpdatedEvent snapshot);

    static readonly IReadOnlyDictionary<StrategyWorkflowStage, PipelineExecutionHandler> _pipelineExecutionMap =
        new Dictionary<StrategyWorkflowStage, PipelineExecutionHandler>
        {
            [StrategyWorkflowStage.RegimeDiscovery] = ExecuteRegimeDiscoveryAsync,
            [StrategyWorkflowStage.MarketCondition] = ExecuteMarketConditionAsync,
            [StrategyWorkflowStage.TradeSelection] = static (context, snapshot) =>
                ExecuteLaterPipelineAsync<StartTradeSelectionPipelineCommand>(
                    context, snapshot, StartTradeSelectionPipelineCommand.Actor,
                    StartTradeSelectionPipelineCommand.Verb, StartTradeSelectionPipelineCommand.ErrorId),
            [StrategyWorkflowStage.OrderComposition] = static (context, snapshot) =>
                ExecuteLaterPipelineAsync<StartOrderCompositionPipelineCommand>(
                    context, snapshot, StartOrderCompositionPipelineCommand.Actor,
                    StartOrderCompositionPipelineCommand.Verb, StartOrderCompositionPipelineCommand.ErrorId),
            [StrategyWorkflowStage.RiskManagement] = static (context, snapshot) =>
                ExecuteLaterPipelineAsync<StartRiskManagementPipelineCommand>(
                    context, snapshot, StartRiskManagementPipelineCommand.Actor,
                    StartRiskManagementPipelineCommand.Verb, StartRiskManagementPipelineCommand.ErrorId)
        };

    /// <inheritdoc />
    protected override ValueTask OnStartup(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
    {
        if (!ActorContext.Options.Enabled)
        {
            ActorContext.Logger.LogInformation(
                "Intrinsic Time Strategy workflow live routing is disabled; no realtime routes were registered");
            return ValueTask.CompletedTask;
        }
        context.AddRealtimeRouter(TriggerRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnShutdown(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
    {
        if (ActorContext.Options.Enabled)
            context.RemoveRealtimeRouter(TriggerRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        IEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(domainEvent, _receiveMap);
        await handler(this, context, domainEvent).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception)
    {
        ActorContext.Logger.LogError(exception,
            "One-way workflow realtime handling failed for {EventName} on {ThreadId}",
            domainEvent?.EventName ?? "Unknown",
            threadId);
        return ValueTask.CompletedTask;
    }

    async ValueTask ExecuteWorkflowAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        FuturesItiSignalGeneratedEvent trigger)
    {
        var workflowId = StrategyWorkflowId.New(ActorContext.TimeProvider);
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(trigger.EntityId);
        var triggerId = trigger.Id == Guid.Empty ? trigger.CommandId : trigger.Id;
        var requestedAtUtc = trigger.CreatedOn == default
            ? ActorContext.TimeProvider.GetUtcNow().UtcDateTime
            : trigger.CreatedOn;
        var resolved = await ActorContext.ConfigurationDb
            .ResolveEffectiveRegimeDiscoveryAsync(requestedAtUtc, trigger.EntityId.TimePeriod).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "No published Regime Discovery parameter set is effective for the workflow trigger.");
        var marketCondition = await ActorContext.ConfigurationDb
            .ResolveEffectiveMarketConditionAsync(requestedAtUtc, ActorContext.Options.FundId, "ES",
                trigger.EntityId.TimePeriod).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "No published Market Condition parameter set is effective for the workflow trigger.");
        if (ActorContext.Options.RequireWarmRegimeDiscoverySignals)
        {
            var readiness = await ActorContext.RegimeDiscoverySnapshotProvider.CaptureAsync(
                RegimeDiscoverySnapshotRequestFactory.Create(
                    MarketSeriesIdentity.ForContract(trigger.EntityId.ContractId),
                    resolved.ParameterSet)).ConfigureAwait(false);
            if (!readiness.IsSuccess)
            {
                ActorContext.Logger.LogWarning(
                    "Regime Discovery live trigger {TriggerId} was not started because {IssueCount} required signal " +
                    "observations did not pass cache warm-up qualification",
                    triggerId,
                    readiness.Issues.Count(issue =>
                        issue.Availability != RegimeDiscoverySignalAvailability.Available));
                return;
            }
        }

        var command = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = triggerId == Guid.Empty
                ? Guid.CreateVersion7(ActorContext.TimeProvider.GetUtcNow())
                : triggerId,
            Subject = CommandSubject(ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, entityId),
            EntityId = entityId,
            ProposedWorkflowId = workflowId,
            TriggerEventId = triggerId,
            TriggerEvent = trigger,
            CorrelationId = trigger.CommandId == Guid.Empty ? triggerId : trigger.CommandId,
            CausationId = triggerId,
            RequestedAtUtc = requestedAtUtc,
            WorkflowDefinitionVersion = 1,
            RegimeDiscoveryParameterSet = resolved.ParameterSet,
            RegimeDiscoveryParameterPayloadSha256 = resolved.PayloadSha256,
            FundId = marketCondition.ParameterSet.FundId,
            MarketConditionParameterSet = marketCondition.ParameterSet,
            MarketConditionParameterPayloadSha256 = marketCondition.PayloadSha256
        };
        await context.SendAsync<ExecuteIntrinsicTimeStrategyWorkflowCommand,
            IntrinsicTimeStrategyWorkflowEntityId>(command, entityId).ConfigureAwait(false);
    }

    static async ValueTask DispatchCommittedStateAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started)
            return;
        if (!_pipelineExecutionMap.TryGetValue(view.CurrentStage, out var execute))
            throw new InvalidOperationException(
                $"No pipeline execution handler is registered for workflow stage {view.CurrentStage}.");
        await execute(context, snapshot).ConfigureAwait(false);
    }

    static async ValueTask ExecuteRegimeDiscoveryAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var timeProvider = RequireEventContext(context).TimeProvider;
        var execute = CreateRegimeExecute(snapshot)
            ?? throw new InvalidOperationException("Only a committed Started/Regime snapshot can dispatch Execute.");
        FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>? terminal;
        try
        {
            using var deadline = new CancellationTokenSource();
            var remaining = execute.ExpiresAtUtc - timeProvider.GetUtcNow().UtcDateTime;
            // ExpiresAtUtc is the calculation deadline enforced inside the Function. The transport receives a
            // short reply-only grace period so caller cancellation cannot race a terminal timeout response.
            deadline.CancelAfter((remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + FunctionReplyGrace);
            var result = await context.RequestFunctionAsync<
                ExecuteRegimeDiscoveryPipelineCommand,
                RegimeDiscoveryExecutionEntityId,
                FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>(
                execute,
                deadline.Token).ConfigureAwait(false);
            terminal = result.Value;
            if (terminal is null || !terminal.IsTerminal)
            {
                terminal = FunctionResult<RegimeDiscoveryPipelineCompletedEvent,
                    RegimeDiscoveryPipelineFailedEvent>.Fail(
                    ExecuteRegimeDiscoveryPipeline.CreateFailedEvent(
                        execute,
                        result.ErrorCode == 0 ? RegimeDiscoveryPipelineFailedEvent.ErrorId : result.ErrorCode,
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Regime Discovery Function returned no terminal result."
                            : result.ErrorMessage,
                        "FunctionRequest",
                        string.Empty,
                        timeProvider.GetUtcNow().UtcDateTime));
            }
        }
        catch (Exception exception)
        {
            terminal = FunctionResult<RegimeDiscoveryPipelineCompletedEvent,
                RegimeDiscoveryPipelineFailedEvent>.Fail(
                ExecuteRegimeDiscoveryPipeline.CreateFailedEvent(
                    execute,
                    RegimeDiscoveryPipelineFailedEvent.ErrorId,
                    "Regime Discovery Function request failed or exceeded its deadline.",
                    "FunctionRequest",
                    exception.GetType().Name,
                    timeProvider.GetUtcNow().UtcDateTime));
        }

        if (terminal.IsCompleted)
        {
            var complete = CreateCompleteCommand(terminal.Completed!);
            await context.SendAsync<CompleteRegimeDiscoveryCommand,
                IntrinsicTimeStrategyWorkflowEntityId>(complete, complete.EntityId).ConfigureAwait(false);
        }
        else
        {
            var fail = CreateFailCommand(terminal.Failed!);
            await context.SendAsync<FailRegimeDiscoveryCommand,
                IntrinsicTimeStrategyWorkflowEntityId>(fail, fail.EntityId).ConfigureAwait(false);
        }
    }

    static async ValueTask ExecuteMarketConditionAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var timeProvider = RequireEventContext(context).TimeProvider;
        var execute = CreateMarketConditionExecute(snapshot)
            ?? throw new InvalidOperationException("Only a committed Started/MarketCondition snapshot can dispatch Execute.");
        FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>? terminal;
        try
        {
            using var deadline = new CancellationTokenSource();
            var remaining = execute.ExpiresAtUtc - timeProvider.GetUtcNow().UtcDateTime;
            var replyGrace = TimeSpan.FromMilliseconds(
                execute.ParameterSet.Execution.TransportReplyGraceMilliseconds);
            deadline.CancelAfter((remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + replyGrace);
            var result = await context.RequestFunctionAsync<
                ExecuteMarketConditionPipelineCommand,
                MarketConditionExecutionEntityId,
                FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>>(
                execute,
                deadline.Token).ConfigureAwait(false);
            terminal = result.Value;
            if (terminal is null || !terminal.IsTerminal)
            {
                terminal = FunctionResult<MarketConditionPipelineCompletedEvent,
                    MarketConditionPipelineFailedEvent>.Fail(
                    ExecuteMarketConditionPipeline.CreateFailedEvent(
                        execute,
                        Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model
                            .MarketConditionFailureCategory.CalculationFailed,
                        Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model
                            .MarketConditionReasonCodes.Calculation,
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Market Condition Function returned no terminal result."
                            : result.ErrorMessage,
                        timeProvider.GetUtcNow().UtcDateTime));
            }
        }
        catch (Exception exception)
        {
            terminal = FunctionResult<MarketConditionPipelineCompletedEvent,
                MarketConditionPipelineFailedEvent>.Fail(
                ExecuteMarketConditionPipeline.CreateFailedEvent(
                    execute,
                    Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model
                        .MarketConditionFailureCategory.CalculationFailed,
                    Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model
                        .MarketConditionReasonCodes.Calculation,
                    $"Market Condition Function request failed: {exception.GetType().Name}.",
                    timeProvider.GetUtcNow().UtcDateTime));
        }

        if (terminal.IsCompleted)
        {
            var complete = CreateMarketConditionCompleteCommand(terminal.Completed!);
            await context.SendAsync<CompleteMarketConditionCommand,
                IntrinsicTimeStrategyWorkflowEntityId>(complete, complete.EntityId).ConfigureAwait(false);
        }
        else
        {
            var fail = CreateMarketConditionFailCommand(terminal.Failed!);
            await context.SendAsync<FailMarketConditionCommand,
                IntrinsicTimeStrategyWorkflowEntityId>(fail, fail.EntityId).ConfigureAwait(false);
        }
    }

    static async ValueTask ExecuteLaterPipelineAsync<TCommand>(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        WorkflowStrategyStateUpdatedEvent snapshot,
        string actor,
        string verb,
        int errorCode)
        where TCommand : class, ICommand<IntrinsicTimeStrategyWorkflowEntityId>, new()
    {
        var view = snapshot.State;
        var commandId = DeterministicPipelineCommandId(
            view.WorkflowId,
            view.CurrentStage,
            view.WorkflowRevision);
        var route = IntrinsicTimeStrategyPipelineRoutes.Get(view.CurrentStage);
        var input = new LaterPipelineInput(view, snapshot.Id, commandId, route.BoundedContext);
        await context.SendAsync<TCommand, IntrinsicTimeStrategyWorkflowEntityId>(
            CreateLaterStart<TCommand>(input, actor, verb, errorCode),
            view.EntityId).ConfigureAwait(false);
    }

    /// <summary>Builds the deterministic Regime Execute command only from a committed Started/Regime snapshot.</summary>
    internal static ExecuteRegimeDiscoveryPipelineCommand? CreateRegimeExecute(
        WorkflowStrategyStateUpdatedEvent snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started ||
            view.CurrentStage != StrategyWorkflowStage.RegimeDiscovery)
            return null;

        var executionId = RegimeDiscoveryExecutionEntityId.Create(view.EntityId, view.WorkflowId);
        return new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = DeterministicPipelineCommandId(
                view.WorkflowId,
                view.CurrentStage,
                view.WorkflowRevision),
            Subject = new ActorSubject(ActorType.Function,
                ExecuteRegimeDiscoveryPipelineCommand.Actor,
                ExecuteRegimeDiscoveryPipelineCommand.Verb,
                executionId.Format()),
            EntityId = executionId,
            InputWorkflowRevision = view.WorkflowRevision,
            WorkflowView = view,
            TriggerEvent = view.TriggerEvent,
            CorrelationId = view.CorrelationId,
            CausationId = snapshot.Id,
            RequestedAtUtc = view.UpdatedAtUtc,
            ExpiresAtUtc = view.ExpiresAtUtc,
            ParameterSet = view.RegimeDiscoveryParameterSet,
            ParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256,
            TargetHorizon = view.TriggerEvent.EntityId.TimePeriod
        };
    }

    /// <summary>Builds the deterministic Market Condition Execute command from committed workflow state.</summary>
    internal static ExecuteMarketConditionPipelineCommand? CreateMarketConditionExecute(
        WorkflowStrategyStateUpdatedEvent snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started ||
            view.CurrentStage != StrategyWorkflowStage.MarketCondition)
            return null;

        var configuredDeadline = view.UpdatedAtUtc.AddMilliseconds(
            view.MarketConditionParameterSet.Execution.MaximumExecutionMilliseconds);
        var executionId = MarketConditionExecutionEntityId.Create(view.EntityId, view.WorkflowId);
        return new ExecuteMarketConditionPipelineCommand
        {
            CommandId = DeterministicPipelineCommandId(
                view.WorkflowId,
                view.CurrentStage,
                view.WorkflowRevision),
            Subject = new ActorSubject(ActorType.Function,
                ExecuteMarketConditionPipelineCommand.Actor,
                ExecuteMarketConditionPipelineCommand.Verb,
                executionId.Format()),
            EntityId = executionId,
            InputWorkflowRevision = view.WorkflowRevision,
            WorkflowView = view,
            TriggerEvent = view.TriggerEvent,
            CorrelationId = view.CorrelationId,
            CausationId = snapshot.Id,
            RequestedAtUtc = view.UpdatedAtUtc,
            ExpiresAtUtc = configuredDeadline <= view.ExpiresAtUtc ? configuredDeadline : view.ExpiresAtUtc,
            ParameterSet = view.MarketConditionParameterSet,
            ParameterPayloadSha256 = view.MarketConditionParameterPayloadSha256,
            TargetHorizon = view.TriggerEvent.EntityId.TimePeriod,
            FundId = view.FundId,
            InstrumentRoot = view.MarketConditionParameterSet.InstrumentRoot
        };
    }

    static TCommand CreateLaterStart<TCommand>(LaterPipelineInput input, string actor, string verb, int errorCode)
        where TCommand : class, ICommand<IntrinsicTimeStrategyWorkflowEntityId>, new()
    {
        var view = input.View;
        var command = new TCommand();
        Set(command, nameof(ICommand.CommandId), input.CommandId);
        Set(command, nameof(ICommand.Subject),
            new ActorSubject(ActorType.Command, actor, verb, view.EntityId.Format()));
        Set(command, "PostEvents", true);
        Set(command, "EntityId", view.EntityId);
        Set(command, nameof(ICommand.ErrorCode), errorCode);
        Set(command, nameof(ICommand.RouteTo), input.BoundedContext);
        Set(command, "WorkflowId", view.WorkflowId);
        Set(command, "InputWorkflowRevision", view.WorkflowRevision);
        Set(command, "WorkflowState", ToLegacyWorkflow(view));
        Set(command, "TriggerEvent", view.TriggerEvent);
        Set(command, "CorrelationId", view.CorrelationId);
        Set(command, "CausationId", input.CausationId);
        Set(command, "RequestedAtUtc", view.UpdatedAtUtc);
        Set(command, "ExpectedCompletionAtUtc", view.ExpiresAtUtc);
        return command;
    }

    static IntrinsicTimeStrategyWorkflowState ToLegacyWorkflow(IntrinsicTimeStrategyWorkflowView view) => new()
    {
        EntityId = view.EntityId,
        WorkflowId = view.WorkflowId,
        TriggerEventId = view.TriggerEventId,
        CorrelationId = view.CorrelationId,
        WorkflowDefinitionVersion = view.WorkflowDefinitionVersion,
        Status = StrategyWorkflowStatus.Running,
        Outcome = view.Outcome,
        CurrentStage = view.CurrentStage,
        WorkflowRevision = view.WorkflowRevision,
        StartedAtUtc = view.StartedAtUtc,
        RegimeDiscovery = view.RegimeDiscovery,
        MarketCondition = view.MarketCondition,
        TradeSelection = view.TradeSelection,
        OrderComposition = view.OrderComposition,
        RiskManagement = view.RiskManagement,
        RegimeDiscoveryParameterSet = view.RegimeDiscoveryParameterSet,
        RegimeDiscoveryParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256,
        FundId = view.FundId,
        MarketConditionParameterSet = view.MarketConditionParameterSet,
        MarketConditionParameterPayloadSha256 = view.MarketConditionParameterPayloadSha256
    };

    static void Set(object target, string property, object? value)
        => EventInitHelper.SetProperty(target, property, value);

    static IIntrinsicTimeStrategyWorkflowRealtimeContext RequireContext(
        IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
        => context as IIntrinsicTimeStrategyWorkflowRealtimeContext
            ?? throw new ArgumentException(
                $"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowRealtimeContext)}.",
                nameof(context));

    static IIntrinsicTimeStrategyWorkflowRealtimeContext RequireEventContext(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
        => context as IIntrinsicTimeStrategyWorkflowRealtimeContext
            ?? throw new ArgumentException(
                $"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowRealtimeContext)}.",
                nameof(context));

    static ActorSubject CommandSubject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId)
        => new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor, verb, entityId.Format());

    internal static CompleteRegimeDiscoveryCommand CreateCompleteCommand(
        RegimeDiscoveryPipelineCompletedEvent completed)
        => new()
        {
            CommandId = DeterministicTerminalCommandId(completed.EntityId, completed.WorkflowId,
                completed.InputWorkflowRevision, completed.Id, CompleteRegimeDiscoveryCommand.Verb),
            Subject = WorkflowSubject(CompleteRegimeDiscoveryCommand.Verb, completed.EntityId),
            EntityId = completed.EntityId,
            WorkflowId = completed.WorkflowId,
            InputWorkflowRevision = completed.InputWorkflowRevision,
            SourceEventId = completed.Id,
            Result = completed.Result,
            CorrelationId = completed.CorrelationId,
            CausationId = completed.Id,
            CompletedAtUtc = completed.CompletedAtUtc
        };

    internal static FailRegimeDiscoveryCommand CreateFailCommand(RegimeDiscoveryPipelineFailedEvent failed)
        => new()
        {
            CommandId = DeterministicTerminalCommandId(failed.EntityId, failed.WorkflowId,
                failed.InputWorkflowRevision, failed.Id, FailRegimeDiscoveryCommand.Verb),
            Subject = WorkflowSubject(FailRegimeDiscoveryCommand.Verb, failed.EntityId),
            EntityId = failed.EntityId,
            WorkflowId = failed.WorkflowId,
            InputWorkflowRevision = failed.InputWorkflowRevision,
            SourceEventId = failed.Id,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = failed.ErrorCode,
                ErrorMessage = failed.ErrorMessage,
                ErrorType = failed.ErrorCode == 23103 ? "Timeout" : failed.ErrorType.ToString(),
                ErrorData = failed.ErrorData,
                FailedAtUtc = failed.ErrorDate
            },
            CorrelationId = failed.CorrelationId,
            CausationId = failed.Id,
            FailedAtUtc = failed.ErrorDate
        };

    internal static CompleteMarketConditionCommand CreateMarketConditionCompleteCommand(
        MarketConditionPipelineCompletedEvent completed)
        => new()
        {
            CommandId = DeterministicTerminalCommandId(completed.EntityId, completed.WorkflowId,
                completed.InputWorkflowRevision, completed.Id, CompleteMarketConditionCommand.Verb),
            Subject = WorkflowSubject(CompleteMarketConditionCommand.Verb, completed.EntityId),
            EntityId = completed.EntityId,
            WorkflowId = completed.WorkflowId,
            InputWorkflowRevision = completed.InputWorkflowRevision,
            SourceEventId = completed.Id,
            Result = completed.Result,
            CorrelationId = completed.CorrelationId,
            CausationId = completed.Id,
            CompletedAtUtc = completed.CompletedAtUtc
        };

    internal static FailMarketConditionCommand CreateMarketConditionFailCommand(
        MarketConditionPipelineFailedEvent failed)
        => new()
        {
            CommandId = DeterministicTerminalCommandId(failed.EntityId, failed.WorkflowId,
                failed.InputWorkflowRevision, failed.Id, FailMarketConditionCommand.Verb),
            Subject = WorkflowSubject(FailMarketConditionCommand.Verb, failed.EntityId),
            EntityId = failed.EntityId,
            WorkflowId = failed.WorkflowId,
            InputWorkflowRevision = failed.InputWorkflowRevision,
            SourceEventId = failed.Id,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = failed.ErrorCode,
                ErrorMessage = failed.ErrorMessage,
                ErrorType = failed.FailureCategory.ToString(),
                ErrorData = failed.ErrorData,
                FailedAtUtc = failed.ErrorDate
            },
            FailureCategory = failed.FailureCategory,
            CorrelationId = failed.CorrelationId,
            CausationId = failed.Id,
            FailedAtUtc = failed.ErrorDate
        };

    static Guid DeterministicPipelineCommandId(
        StrategyWorkflowId workflowId,
        StrategyWorkflowStage stage,
        long revision)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{workflowId}|{stage}|{revision}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    internal static Guid DeterministicTerminalCommandId(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowId workflowId,
        long revision,
        Guid sourceEventId,
        string verb)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{entityId.Format()}|{workflowId}|{revision}|{sourceEventId:N}|{verb}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    static ActorSubject WorkflowSubject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId)
        => new(ActorType.Command, CompleteRegimeDiscoveryCommand.Actor, verb, entityId.Format());

    readonly record struct LaterPipelineInput(
        IntrinsicTimeStrategyWorkflowView View,
        Guid CausationId,
        Guid CommandId,
        BoundedContextName BoundedContext);
}
