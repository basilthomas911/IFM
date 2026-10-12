using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using System.Globalization;
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
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using System.Diagnostics;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Logging;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
/// <summary>Executes pipeline work only from committed Strategy Workflow state snapshots.</summary>
/// <remarks>
/// This stateless actor consumes committed notifications, including explicit workflow redispatch. For Regime Discovery it owns the direct Function
/// request and translates the typed terminal reply into a Strategy Workflow complete or fail command.
/// </remarks>
public sealed partial class IntrinsicTimeStrategyWorkflowRealtimeActor(IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> actorContext) : BaseEventActor<IntrinsicTimeStrategyWorkflowRealtimeActor>(actorContext, RequireContext(actorContext).Logger)
{
    static readonly TimeSpan FunctionReplyGrace = TimeSpan.FromSeconds(5);
    /// <summary>Gets the workflow Realtime actor name.</summary>
    public const string ActorName = "IntrinsicTimeStrategyWorkflowRealtime";
    IIntrinsicTimeStrategyWorkflowRealtimeContext ActorContext { get; } = RequireContext(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [WorkflowStrategyStateUpdatedEvent.Verb] = message => message.AsEvent<WorkflowStrategyStateUpdatedEvent>()!
    };
    static readonly IReadOnlyDictionary<Type, Func<IntrinsicTimeStrategyWorkflowRealtimeActor, IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>, IEvent, ValueTask>> _receiveMap = new Dictionary<Type, Func<IntrinsicTimeStrategyWorkflowRealtimeActor, IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>, IEvent, ValueTask>>
    {
        [typeof(WorkflowStrategyStateUpdatedEvent)] = static (actor, context, @event) => ((WorkflowStrategyStateUpdatedEvent)@event).ExecuteAsync(actor, context)
    };
    delegate ValueTask PipelineExecutionHandler(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot);
    static readonly Dictionary<StrategyWorkflowStage, PipelineExecutionHandler> _pipelineExecutionMap = new Dictionary<StrategyWorkflowStage, PipelineExecutionHandler>
    {
        [StrategyWorkflowStage.RegimeDiscovery] = ExecuteRegimeDiscoveryAsync,
        [StrategyWorkflowStage.MarketCondition] = ExecuteMarketConditionAsync,
        [StrategyWorkflowStage.TradeSelection] = static (context, snapshot) => snapshot.State.CompositionHandoff?.Status == CompositionHandoffStatus.ReservationPending ? ReserveSelectionAsync(context, snapshot) : ExecuteSelectionAsync(context, snapshot),
        [StrategyWorkflowStage.OrderComposition] = static (context, snapshot) => OrderComposer.Realtime.ExecuteOrderComposition.ExecuteAsync(snapshot, RequireEventContext(context)),
        [StrategyWorkflowStage.RiskManagement] = static (context, snapshot) => RiskManager.Realtime.ExecuteRiskManagement.ExecuteAsync(snapshot, RequireEventContext(context))
    };
    /// <inheritdoc/>
    protected override ValueTask OnStartup(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
    {
        _ = context;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask OnShutdown(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context) => OnStartup(context);

    /// <inheritdoc/>
    protected override IEvent ParseMessage(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);
    /// <inheritdoc/>
    protected override async ValueTask ReceiveAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        await handler(this, context, @event).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override ValueTask OnExceptionAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var snapshot = @event as WorkflowStrategyStateUpdatedEvent;
        IntrinsicTimeStrategyWorkflowLogging.StageHandlerFailed(ActorContext.Logger, ex, @event?.Id ?? Guid.Empty, snapshot?.WorkflowId.ToString() ?? string.Empty, snapshot?.EntityId.Format() ?? threadId.ToString(), snapshot?.WorkflowRevision ?? 0, snapshot?.State.CurrentStage.ToString() ?? "Unknown", snapshot is null ? @event?.EventName ?? "Unknown" : HandlerName(snapshot.State.CurrentStage), ex.GetType().Name, ex.Message);
        return ValueTask.CompletedTask;
    }

    internal static async ValueTask DispatchCommittedStateAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        using var trace = WorkflowTrace.Start("workflow.dispatch", snapshot.State);
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started)
            return;
        if (view.CurrentStage is StrategyWorkflowStage.TradeSelection or StrategyWorkflowStage.OrderComposition)
        {
            var read = new RedispatchCurrentStrategyPipelineCommand
            {
                EntityId = view.EntityId,
                Subject = WorkflowSubject(RedispatchCurrentStrategyPipelineCommand.Verb, view.EntityId)
            };
            var current = await RequireEventContext(context).WorkflowRepository.LoadStateAsync(read).ConfigureAwait(false);
            if (!IsCurrentSelectionNotification(view, current.CurrentView))
                return;
        }

        if (!_pipelineExecutionMap.TryGetValue(view.CurrentStage, out var execute))
            throw new InvalidOperationException($"No pipeline execution handler is registered for workflow stage {view.CurrentStage}.");
        var logger = RequireEventContext(context).Logger;
        var handlerName = HandlerName(view.CurrentStage);
        if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
        {
            var loggingArgument1 = view.WorkflowId.ToString();
            var loggingArgument2 = view.EntityId.Format();
            var loggingArgument3 = view.CurrentStage.ToString();
            IntrinsicTimeStrategyWorkflowLogging.StageDispatched(logger, snapshot.Id, loggingArgument1, loggingArgument2, view.WorkflowRevision, loggingArgument3, handlerName);
        }

        var started = Stopwatch.GetTimestamp();
        await execute(context, snapshot).ConfigureAwait(false);
        if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
        {
            var loggingArgument1 = view.WorkflowId.ToString();
            var loggingArgument2 = view.EntityId.Format();
            var loggingArgument3 = view.CurrentStage.ToString();
            var loggingArgument4 = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            IntrinsicTimeStrategyWorkflowLogging.StageHandlerCompleted(logger, snapshot.Id, loggingArgument1, loggingArgument2, view.WorkflowRevision, loggingArgument3, handlerName, loggingArgument4);
        }
    }

    static string HandlerName(StrategyWorkflowStage stage) => stage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => "RegimeDiscovery",
        StrategyWorkflowStage.MarketCondition => "MarketCondition",
        StrategyWorkflowStage.TradeSelection => "TradeSelection",
        StrategyWorkflowStage.OrderComposition => "OrderComposition",
        StrategyWorkflowStage.RiskManagement => "RiskManagement",
        _ => "Unknown"
    };
    internal static StrategyWorkflowStageState CurrentStageState(IntrinsicTimeStrategyWorkflowView view) => view.CurrentStage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => view.RegimeDiscovery,
        StrategyWorkflowStage.MarketCondition => view.MarketCondition,
        StrategyWorkflowStage.TradeSelection => view.TradeSelection,
        StrategyWorkflowStage.OrderComposition => view.OrderComposition,
        StrategyWorkflowStage.RiskManagement => view.RiskManagement,
        _ => new StrategyWorkflowStageState()};
    internal static bool IsCurrentSelectionNotification(IntrinsicTimeStrategyWorkflowView notification, IntrinsicTimeStrategyWorkflowView? current) => current is { Status: WorkflowStrategyMachineStatus.Started } && current.EntityId == notification.EntityId && current.WorkflowId == notification.WorkflowId && current.WorkflowRevision == notification.WorkflowRevision && current.CurrentStage == notification.CurrentStage && current.CompositionHandoff?.Status == notification.CompositionHandoff?.Status;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A failed or timed-out Function request is converted to the recorded terminal pipeline failure for this workflow.")]
    static async ValueTask ExecuteRegimeDiscoveryAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var timeProvider = RequireEventContext(context).TimeProvider;
        var execute = CreateRegimeExecute(snapshot) ?? throw new InvalidOperationException("Only a committed Started/Regime snapshot can dispatch Execute.");
        FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>? terminal;
        try
        {
            var start = await execute.StartPipelineAsync(RequireEventContext(context)).ConfigureAwait(false);
            if (!start.Success)
            {
                await FailRegimeDiscoveryInitializationAsync(context, snapshot, start.Error!).ConfigureAwait(false);
                return;
            }

            execute = start.Value!;
            using var deadline = new CancellationTokenSource();
            var remaining = execute.ExpiresAtUtc - timeProvider.GetUtcNow().UtcDateTime;
            // ExpiresAtUtc is the calculation deadline enforced inside the Function. The transport receives a
            // short reply-only grace period so caller cancellation cannot race a terminal timeout response.
            deadline.CancelAfter((remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + FunctionReplyGrace);
            var result = await context.RequestFunctionAsync<ExecuteRegimeDiscoveryPipelineCommand, RegimeDiscoveryExecutionEntityId, FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>(execute, deadline.Token).ConfigureAwait(false);
            terminal = result.Value;
            if (terminal is null || !terminal.IsTerminal)
            {
                terminal = RegimeDiscoveryFunctionActor.MapEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent), execute, new RegimeDiscoveryExecutionFailed(timeProvider.GetUtcNow().UtcDateTime, string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Regime Discovery Function returned no terminal result." : result.ErrorMessage, "FunctionRequest", result.ErrorCode == 0 ? RegimeDiscoveryPipelineFailedEvent.ErrorId : result.ErrorCode, [], Guid.Empty)), timeProvider);
            }
        }
        catch (Exception exception)
        {
            terminal = RegimeDiscoveryFunctionActor.MapEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent), execute, new RegimeDiscoveryExecutionFailed(timeProvider.GetUtcNow().UtcDateTime, PipelineExceptionDiagnostics.Summary("Regime Discovery Function request failed or exceeded its deadline", exception), "FunctionRequest", RegimeDiscoveryPipelineFailedEvent.ErrorId, [], Guid.Empty, PipelineExceptionDiagnostics.Format(exception, new Dictionary<string, string>(StringComparer.Ordinal) { ["WorkflowId"] = execute.WorkflowId.ToString(), ["TargetHorizon"] = execute.TargetHorizon.ToString(), ["ExpiresAtUtc"] = execute.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture) }))), timeProvider);
        }

        if (terminal.IsCompleted)
        {
            var complete = CreateCompleteCommand(terminal.Completed!);
            await context.SendAsync<CompleteRegimeDiscoveryCommand, IntrinsicTimeStrategyWorkflowEntityId>(complete, complete.EntityId).ConfigureAwait(false);
        }
        else
        {
            var fail = CreateFailCommand(terminal.Failed!, execute.ParameterSet.ParameterSetId, execute.ParameterSet.Version, execute.ParameterPayloadSha256);
            await context.SendAsync<FailRegimeDiscoveryCommand, IntrinsicTimeStrategyWorkflowEntityId>(fail, fail.EntityId).ConfigureAwait(false);
        }
    }

    static async ValueTask FailRegimeDiscoveryInitializationAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot, PipelineInitializationError error)
    {
        var view = snapshot.State;
        var now = RequireEventContext(context).TimeProvider.GetUtcNow().UtcDateTime;
        var diagnostic = string.Join(';', error.ReasonCodes.Concat(error.DiagnosticData.Select(pair => $"{pair.Key}={pair.Value}")));
        var fail = new FailRegimeDiscoveryCommand
        {
            CommandId = DeterministicTerminalCommandId(view.EntityId, view.WorkflowId, view.WorkflowRevision, snapshot.Id, FailRegimeDiscoveryCommand.Verb),
            Subject = WorkflowSubject(FailRegimeDiscoveryCommand.Verb, view.EntityId),
            EntityId = view.EntityId,
            WorkflowId = view.WorkflowId,
            InputWorkflowRevision = view.WorkflowRevision,
            SourceEventId = snapshot.Id,
            CorrelationId = view.CorrelationId,
            CausationId = snapshot.Id,
            FailedAtUtc = now,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = 23102,
                ErrorType = error.ErrorType,
                ErrorMessage = error.Message,
                ErrorData = diagnostic,
                FailedAtUtc = now
            },
            ParameterSetId = error.ParameterSetId,
            ParameterSetVersion = error.ParameterSetVersion,
            ParameterPayloadSha256 = error.ParameterPayloadSha256
        };
        await context.SendAsync<FailRegimeDiscoveryCommand, IntrinsicTimeStrategyWorkflowEntityId>(fail, fail.EntityId).ConfigureAwait(false);
    }

    static async ValueTask ExecuteMarketConditionAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var start = await snapshot.StartPipelineAsync(RequireEventContext(context)).ConfigureAwait(false);
        if (start.Success)
        {
            await ExecuteAssessmentAsync(context, start.Value!).ConfigureAwait(false);
            return;
        }

        var view = snapshot.State;
        var now = RequireEventContext(context).TimeProvider.GetUtcNow().UtcDateTime;
        var error = start.Error!;
        var fail = new FailMarketConditionCommand
        {
            CommandId = DeterministicTerminalCommandId(view.EntityId, view.WorkflowId, view.WorkflowRevision, snapshot.Id, FailMarketConditionCommand.Verb),
            Subject = WorkflowSubject(FailMarketConditionCommand.Verb, view.EntityId),
            EntityId = view.EntityId,
            WorkflowId = view.WorkflowId,
            InputWorkflowRevision = view.WorkflowRevision,
            SourceEventId = snapshot.Id,
            CorrelationId = view.CorrelationId,
            CausationId = snapshot.Id,
            FailedAtUtc = now,
            FailureCategory = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketConditionFailureCategory.ContractInvalid,
            Failure = new()
            {
                ErrorCode = MarketConditionAssessmentFailedEvent.ErrorId,
                ErrorMessage = error.Message,
                ErrorType = error.ErrorType,
                ErrorData = string.Join(';', error.ReasonCodes.Concat(error.DiagnosticData.Select(pair => $"{pair.Key}={pair.Value}"))),
                FailedAtUtc = now
            }
        };
        await context.SendAsync<FailMarketConditionCommand, IntrinsicTimeStrategyWorkflowEntityId>(fail, fail.EntityId).ConfigureAwait(false);
    }

    /// <summary>Builds the deterministic Regime Execute command only from a committed Started/Regime snapshot.</summary>
    internal static ExecuteRegimeDiscoveryPipelineCommand? CreateRegimeExecute(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started || view.CurrentStage != StrategyWorkflowStage.RegimeDiscovery)
            return null;
        var executionId = RegimeDiscoveryExecutionEntityId.Create(view.EntityId, view.WorkflowId);
        return new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = DeterministicPipelineCommandId(view.WorkflowId, view.CurrentStage, view.WorkflowRevision),
            Subject = new ActorSubject(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor, ExecuteRegimeDiscoveryPipelineCommand.Verb, executionId.Format()),
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
    static IIntrinsicTimeStrategyWorkflowRealtimeContext RequireContext(IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context) => context as IIntrinsicTimeStrategyWorkflowRealtimeContext ?? throw new ArgumentException($"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowRealtimeContext)}.", nameof(context));
    internal static IIntrinsicTimeStrategyWorkflowRealtimeContext RequireEventContext(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context) => context as IIntrinsicTimeStrategyWorkflowRealtimeContext ?? throw new ArgumentException($"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowRealtimeContext)}.", nameof(context));
    internal static CompleteRegimeDiscoveryCommand CreateCompleteCommand(RegimeDiscoveryPipelineCompletedEvent completed) => new()
    {
        CommandId = DeterministicTerminalCommandId(completed.EntityId, completed.WorkflowId, completed.InputWorkflowRevision, completed.Id, CompleteRegimeDiscoveryCommand.Verb),
        Subject = WorkflowSubject(CompleteRegimeDiscoveryCommand.Verb, completed.EntityId),
        EntityId = completed.EntityId,
        WorkflowId = completed.WorkflowId,
        InputWorkflowRevision = completed.InputWorkflowRevision,
        SourceEventId = completed.Id,
        Result = completed.Result,
        CorrelationId = completed.CorrelationId,
        CausationId = completed.Id,
        CompletedAtUtc = completed.CompletedAtUtc,
        ParameterSet = completed.ParameterSet,
        ParameterApplication = completed.ParameterApplication,
        ParameterPayloadSha256 = completed.ParameterPayloadSha256
    };
    internal static FailRegimeDiscoveryCommand CreateFailCommand(RegimeDiscoveryPipelineFailedEvent failed, Guid parameterSetId = default, int parameterSetVersion = 0, string parameterPayloadSha256 = "") => new()
    {
        CommandId = DeterministicTerminalCommandId(failed.EntityId, failed.WorkflowId, failed.InputWorkflowRevision, failed.Id, FailRegimeDiscoveryCommand.Verb),
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
        FailedAtUtc = failed.ErrorDate,
        ParameterSetId = parameterSetId,
        ParameterSetVersion = parameterSetVersion,
        ParameterPayloadSha256 = parameterPayloadSha256
    };
    static Guid DeterministicPipelineCommandId(StrategyWorkflowId workflowId, StrategyWorkflowStage stage, long revision)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{workflowId}|{stage}|{revision}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    internal static Guid DeterministicTerminalCommandId(IntrinsicTimeStrategyWorkflowEntityId entityId, StrategyWorkflowId workflowId, long revision, Guid sourceEventId, string verb)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{entityId.Format()}|{workflowId}|{revision}|{sourceEventId:N}|{verb}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    static ActorSubject WorkflowSubject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId) => new(ActorType.Command, CompleteRegimeDiscoveryCommand.Actor, verb, entityId.Format());
}
