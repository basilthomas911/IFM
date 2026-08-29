using System.Diagnostics;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Extensions;

public static class ExecuteMarketConditionPipeline
{
    public static async ValueTask<FunctionResult<MarketConditionPipelineCompletedEvent,
        MarketConditionPipelineFailedEvent>> ExecuteAsync(this ExecuteMarketConditionPipelineCommand command,
        IFunctionActorContext<MarketConditionFunctionActor> context, CancellationToken token = default)
    {
        var typed = context as IMarketConditionFunctionContext
            ?? throw new InvalidOperationException($"{nameof(context)} must implement {nameof(IMarketConditionFunctionContext)}.");
        return await ExecuteAtomicAsync(command, typed.TimeProvider,
            ct => CaptureAndCalculateAsync(command, typed, ct),
            (delay, ct) => Task.Delay(delay, typed.TimeProvider, ct), token).ConfigureAwait(false);
    }

    internal static async Task<FunctionResult<MarketConditionPipelineCompletedEvent,
        MarketConditionPipelineFailedEvent>> ExecuteAtomicAsync(
        ExecuteMarketConditionPipelineCommand command, TimeProvider clock,
        Func<CancellationToken, Task<MarketConditionExecutionOutcome>> worker,
        Func<TimeSpan, CancellationToken, Task> delay, CancellationToken token = default)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = MarketConditionTelemetry.Start("market-condition.function-request");
        var now = UtcNow(clock);
        var configuredDeadline = command.RequestedAtUtc.AddMilliseconds(
            command.ParameterSet.Execution.MaximumExecutionMilliseconds);
        var deadline = new[] { command.ExpiresAtUtc, command.WorkflowView.ExpiresAtUtc, configuredDeadline }.Min();
        if (now >= deadline) return FailedWithTelemetry(command, Timeout(now), started);
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var workerTask = worker(workerCts.Token);
        var timer = delay(deadline - now, timerCts.Token);
        var winner = await Task.WhenAny(workerTask, timer).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (winner == timer)
        {
            workerCts.Cancel(); _ = ObserveAsync(workerTask);
            return FailedWithTelemetry(command, Timeout(UtcNow(clock)), started);
        }
        timerCts.Cancel(); var result = await workerTask.ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (UtcNow(clock) >= deadline)
            return FailedWithTelemetry(command, Timeout(UtcNow(clock)), started);
        return result switch
        {
            MarketConditionExecutionCompleted completed => Completed(command, completed.Result),
            MarketConditionExecutionFailed failed => FailedWithTelemetry(command, failed, started),
            _ => throw new InvalidOperationException("Unknown Market Condition execution outcome.")
        };
    }

    static async Task<MarketConditionExecutionOutcome> CaptureAndCalculateAsync(
        ExecuteMarketConditionPipelineCommand command, IMarketConditionFunctionContext context, CancellationToken token)
    {
        try
        {
            var captured = await context.SnapshotProvider.CaptureAsync(command, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (captured.Outcome == MarketConditionCaptureOutcome.Failed)
                return new MarketConditionExecutionFailed(UtcNow(context.TimeProvider), captured.FailureCategory,
                    captured.ReasonCode, captured.SafeMessage, captured.Snapshot.SnapshotId);
            var envelope = command.WorkflowView.RegimeDiscovery.Result
                ?? throw new MarketConditionCalculationException(MarketConditionFailureCategory.ContractInvalid,
                    MarketConditionReasonCodes.ContractInvalid, "The accepted Regime result is missing.");
            var regime = MessagePackSerializer.Deserialize<RegimeDiscoveryResult>(envelope.Payload);
            var result = context.CalculationModel.Calculate(new MarketConditionCalculationInput
            {
                ResultId = command.CommandId, InputWorkflowRevision = command.InputWorkflowRevision,
                WorkflowView = command.WorkflowView, TriggerEvent = command.TriggerEvent,
                RegimeResult = regime, ParameterSet = command.ParameterSet, Snapshot = captured.Snapshot
            });
            token.ThrowIfCancellationRequested();
            return new MarketConditionExecutionCompleted(result);
        }
        catch (MarketConditionCalculationException ex)
        {
            return new MarketConditionExecutionFailed(UtcNow(context.TimeProvider), ex.Category,
                ex.ReasonCode, ex.Message, Guid.Empty);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new MarketConditionExecutionFailed(UtcNow(context.TimeProvider),
                MarketConditionFailureCategory.CalculationFailed, MarketConditionReasonCodes.Calculation,
                $"Market Condition calculation failed: {ex.GetType().Name}.", Guid.Empty);
        }
    }

    static FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent> Completed(
        ExecuteMarketConditionPipelineCommand c, MarketConditionResult result)
    {
        var payload = MessagePackSerializer.Serialize(result);
        return FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>.Complete(new()
        {
            Subject = Subject(MarketConditionPipelineCompletedEvent.Verb, c), Id = result.ResultId,
            EntityId = c.WorkflowEntityId, CommandId = c.CommandId, AggregateId = c.EntityId.Format(),
            EventSource = $"{ExecuteMarketConditionPipelineCommand.Actor}Actor", ReceivedOn = result.EvaluatedAtUtc,
            WorkflowId = c.WorkflowId, InputWorkflowRevision = c.InputWorkflowRevision,
            CorrelationId = c.CorrelationId, CausationId = c.CausationId,
            PipelineStage = StrategyWorkflowStage.MarketCondition,
            Result = StrategyStageResultEnvelope.Create(result.ResultId, nameof(MarketConditionResult),
                MarketConditionResult.CurrentSchemaVersion, payload, result.MarketDataAsOfUtc, result.EvaluatedAtUtc),
            CompletedAtUtc = result.EvaluatedAtUtc, ExpiresAtUtc = c.ExpiresAtUtc,
            ParameterPayloadSha256 = c.ParameterPayloadSha256, MarketConditionSnapshotId = result.SnapshotId,
            EvaluatedAtUtc = result.EvaluatedAtUtc, ValidUntilUtc = result.ValidUntilUtc
        });
    }
    static FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent> Failed(
        ExecuteMarketConditionPipelineCommand c, MarketConditionExecutionFailed f)
        => FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>.Fail(
            CreateFailedEvent(c, f.Category, f.ReasonCode, f.Message, f.FailedAtUtc, f.SnapshotId));
    static FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent> FailedWithTelemetry(
        ExecuteMarketConditionPipelineCommand command, MarketConditionExecutionFailed failure, long started)
    {
        MarketConditionTelemetry.RecordFailure(failure.Category, failure.ReasonCode, command.TargetHorizon,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return Failed(command, failure);
    }
    internal static MarketConditionPipelineFailedEvent CreateFailedEvent(
        ExecuteMarketConditionPipelineCommand c, MarketConditionFailureCategory category, string reason,
        string message, DateTime at, Guid snapshotId = default) => new()
        {
            Subject = Subject(MarketConditionPipelineFailedEvent.Verb, c), EntityId = c.WorkflowEntityId,
            Id = Guid.CreateVersion7(new DateTimeOffset(at, TimeSpan.Zero)), ErrorDate = at,
            CommandId = c.CommandId, EventSource = $"{ExecuteMarketConditionPipelineCommand.Actor}Actor",
            ErrorMessage = message, ErrorCode = MarketConditionPipelineFailedEvent.ErrorId,
            ErrorType = ErrorType.Command, ErrorData = reason, ReceivedOn = at,
            AggregateId = c.EntityId.Format(), CommandName = c.CommandName, RouteTo = c.RouteTo.ToString(),
            WorkflowId = c.WorkflowId, InputWorkflowRevision = c.InputWorkflowRevision,
            CorrelationId = c.CorrelationId, CausationId = c.CausationId,
            PipelineStage = StrategyWorkflowStage.MarketCondition, ExpiresAtUtc = c.ExpiresAtUtc,
            FailureCategory = category, MarketConditionSnapshotId = snapshotId,
            ParameterPayloadSha256 = c.ParameterPayloadSha256, ProcessingStarted = c.RequestedAtUtc
        };
    static MarketConditionExecutionFailed Timeout(DateTime now) => new(now,
        MarketConditionFailureCategory.Timeout, MarketConditionReasonCodes.Timeout,
        "Market Condition exceeded its fixed calculation deadline.", Guid.Empty);
    static ActorSubject Subject(string verb, ExecuteMarketConditionPipelineCommand c)
        => new(ActorType.Function, ExecuteMarketConditionPipelineCommand.Actor, verb, c.EntityId.Format());
    static DateTime UtcNow(TimeProvider p) => p.GetUtcNow().UtcDateTime;
    static async Task ObserveAsync(Task<MarketConditionExecutionOutcome> task) { try { await task.ConfigureAwait(false); } catch { } }
}

internal abstract record MarketConditionExecutionOutcome;
internal sealed record MarketConditionExecutionCompleted(MarketConditionResult Result) : MarketConditionExecutionOutcome;
internal sealed record MarketConditionExecutionFailed(DateTime FailedAtUtc, MarketConditionFailureCategory Category,
    string ReasonCode, string Message, Guid SnapshotId) : MarketConditionExecutionOutcome;
