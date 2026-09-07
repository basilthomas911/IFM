using System.Diagnostics;
using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;

/// <summary>A typed verb handler on the existing Function mailbox, with no independent actor or publication.</summary>
public sealed class MarketConditionAssessmentHandler(
    IMarketConditionAssessmentSnapshotProvider snapshots,
    IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand> repository,
    IFunctionProjector<MarketConditionAssessmentCompletedEvent> projector,
    ILogger<MarketConditionAssessmentHandler> logger, TimeProvider? timeProvider = null)
{
    readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    readonly MarketConditionAssessmentCalculator _calculator = new();

    public async ValueTask HandleAsync(IFunctionActorContext context, IActorMessage message, ActorThreadId threadId, CancellationToken cancellationToken)
    {
        ExecuteMarketConditionAssessmentCommand? command = null;
        FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent> terminal;
        try
        {
            try
            {
                if (message.Subject.ActorType != ActorType.Function || message.Subject.Name != ExecuteMarketConditionAssessmentCommand.Actor ||
                    message.Subject.Verb != ExecuteMarketConditionAssessmentCommand.Verb)
                    throw new ArgumentException("Market Condition supports only Assess requests.");
                command = message.AsCommand<ExecuteMarketConditionAssessmentCommand>() ?? throw new ArgumentException("Missing assessment request.");
            }
            finally { message.ReleasePayload(); }
            if (message.Subject.ActorType != ActorType.Function || message.Subject.Name != ExecuteMarketConditionAssessmentCommand.Actor || message.Subject.Verb != ExecuteMarketConditionAssessmentCommand.Verb || message.Subject.EntityId != command.EntityId.Format())
                throw new ArgumentException("Assessment transport subject mismatch.");
            terminal = await ExecuteAsync(context, command, threadId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Assessment request parsing or validation failed");
            terminal = FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Fail(Failed(command, MarketConditionFailureCategory.ContractInvalid, "MC.ASSESSMENT.CONTRACT_INVALID"));
        }
        ServiceResult<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> reply = terminal.IsCompleted
            ? new ServiceOk<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>(terminal)
            : new ServiceFailed<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>(terminal.Failed!.ErrorCode, terminal.Failed.ErrorMessage, terminal);
        await message.ReplyAsync(reply).ConfigureAwait(false);
    }

    public async Task<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> ExecuteAsync(
        IFunctionActorContext context, ExecuteMarketConditionAssessmentCommand c, ActorThreadId threadId, CancellationToken cancellationToken = default)
    {
        var stage = MarketConditionFailureCategory.ContractInvalid;
        var started = Stopwatch.GetTimestamp();
        using var activity = MarketConditionTelemetry.Start("market-condition.assessment");
        activity?.SetTag("workflow.id",c.WorkflowId.ToString());
        activity?.SetTag("correlation.id",c.CorrelationId.ToString());
        using var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            MarketConditionAssessmentContracts.ValidateRequest(c);
            stage = MarketConditionFailureCategory.PersistenceFailed;
            // A completed invocation remains replayable after its market authority expires. Workflow/selector
            // consumers independently reject expired authority; retry must not recapture a different market.
            var state = await repository.LoadStateAsync(c,cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromMilliseconds(c.ParameterSet.MaximumExecutionMilliseconds),_clock,cancellationToken).ConfigureAwait(false);
            state.Id = threadId;
            if (state.IsCompleted)
                return state.Matches(c) && state.CompletedEvent is { } prior
                    ? FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Complete(prior)
                    : FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Fail(Failed(c, MarketConditionFailureCategory.ContractInvalid, "MC.ASSESSMENT.CONFLICTING_DUPLICATE"));
            var remaining = c.ExpiresAtUtc - _clock.GetUtcNow().UtcDateTime;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException();
            var worker = RunAsync();
            try { return await worker.WaitAsync(remaining, _clock, cancellationToken).ConfigureAwait(false); }
            catch { workerCancellation.Cancel(); _ = ObserveAsync(worker); throw; }

            async Task<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> RunAsync()
            {
                var ct = workerCancellation.Token;
                void Fence()
                {
                    ct.ThrowIfCancellationRequested();
                    if (_clock.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc) throw new TimeoutException();
                }
                Fence();
                stage = MarketConditionFailureCategory.RequiredInputInvalid;
                var snapshot = await snapshots.CaptureAsync(c.ParameterSet, _clock.GetUtcNow().UtcDateTime, ct).ConfigureAwait(false);
                Fence();
                stage = MarketConditionFailureCategory.CalculationFailed;
                var result = _calculator.Calculate(c, snapshot, c.CommandId);
                Fence();
                var completed = new MarketConditionAssessmentCompletedEvent
                {
                    Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, MarketConditionAssessmentCompletedEvent.Verb, c.EntityId.Format()),
                    Id = result.ResultId, EntityId = c.WorkflowEntityId, CommandId = c.CommandId, AggregateId = c.EntityId.Format(),
                    EventSource = $"{ExecuteMarketConditionAssessmentCommand.Actor}Actor", ReceivedOn = result.EvaluatedAtUtc,
                    WorkflowId = c.WorkflowId, InputWorkflowRevision = c.InputWorkflowRevision, CorrelationId = c.CorrelationId, CausationId = c.CausationId,
                    PipelineStage = StrategyWorkflowStage.MarketCondition, Result = StrategyStageResultEnvelope.Create(result.ResultId,
                        nameof(MarketConditionAssessmentResult), 1, MessagePackSerializer.Serialize(result), snapshot.EvaluatedAtUtc, result.EvaluatedAtUtc),
                    CompletedAtUtc = _clock.GetUtcNow().UtcDateTime, ExpiresAtUtc = c.ExpiresAtUtc, ParameterPayloadSha256 = c.ParameterPayloadSha256,
                    MarketConditionSnapshotId = snapshot.SnapshotId, EvaluatedAtUtc = result.EvaluatedAtUtc, ValidUntilUtc = result.Assessment.ValidUntilUtc,
                    RequestFingerprint = c.Fingerprint(), Snapshot = snapshot
                };
                stage = MarketConditionFailureCategory.ProjectionFailed;
                Fence();
                await projector.ProjectAsync(completed, ct).ConfigureAwait(false);
                Fence();
                stage = MarketConditionFailureCategory.PersistenceFailed;
                if (!state.TryComplete(completed, c)) throw new InvalidOperationException("Assessment completion transition rejected.");
                await repository.SaveCompletedStateAsync(context, state, c, ct).ConfigureAwait(false);
                Fence();
                logger.LogInformation("Assessment completed Workflow={WorkflowId} Profile={Profile} Horizon={Horizon} Availability={Availability} ElapsedMs={ElapsedMs}", c.WorkflowId, c.MarketProfileId, c.TargetHorizon, result.Assessment.Availability, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                MarketConditionTelemetry.RecordAssessment(result,Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                return FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Complete(completed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var category = ex is TimeoutException ? MarketConditionFailureCategory.Timeout : stage;
            logger.LogError(ex, "Assessment failed at {Stage}, Workflow={WorkflowId}, Horizon={Horizon}", category, c.WorkflowId, c.TargetHorizon);
            MarketConditionTelemetry.RecordFailure(category, $"MC.ASSESSMENT.{category.ToString().ToUpperInvariant()}", c.TargetHorizon, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Fail(Failed(c, category, $"MC.ASSESSMENT.{category.ToString().ToUpperInvariant()}"));
        }
    }
    MarketConditionAssessmentFailedEvent Failed(ExecuteMarketConditionAssessmentCommand? c, MarketConditionFailureCategory category, string reason)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return new()
        {
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, MarketConditionAssessmentFailedEvent.Verb, c?.EntityId.Format() ?? ""),
            Id = Guid.NewGuid(), EntityId = c?.WorkflowEntityId ?? default, WorkflowId = c?.WorkflowId ?? default,
            CommandId = c?.CommandId ?? Guid.Empty, InputWorkflowRevision = c?.InputWorkflowRevision ?? 0,
            ErrorDate = now, ReceivedOn = now, ErrorCode = MarketConditionAssessmentFailedEvent.ErrorId,
            ErrorType = ErrorType.Command, ErrorData = reason, ErrorMessage = $"Market assessment failed: {category}.",
            EventSource = $"{ExecuteMarketConditionAssessmentCommand.Actor}Actor", AggregateId = c?.EntityId.Format() ?? "",
            CommandName = nameof(ExecuteMarketConditionAssessmentCommand), RouteTo = c?.RouteTo.ToString() ?? "",
            CorrelationId = c?.CorrelationId ?? Guid.Empty, CausationId = c?.CausationId ?? Guid.Empty,
            PipelineStage = StrategyWorkflowStage.MarketCondition, FailureCategory = category,
            ExpiresAtUtc = c?.ExpiresAtUtc ?? default, ParameterPayloadSha256 = c?.ParameterPayloadSha256 ?? "", ProcessingStarted = c?.RequestedAtUtc ?? now
        };
    }
    static async Task ObserveAsync(Task task) { try { await task.ConfigureAwait(false); } catch { /* Failure is already returned; a late worker is fenced from further writes. */ } }
}
