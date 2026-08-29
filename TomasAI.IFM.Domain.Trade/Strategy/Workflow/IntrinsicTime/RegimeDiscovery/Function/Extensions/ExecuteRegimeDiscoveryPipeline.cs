using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Extensions;

/// <summary>Produces one completed candidate or one non-durable failed Function response before the deadline.</summary>
public static class ExecuteRegimeDiscoveryPipeline
{
    public static async ValueTask<FunctionResult<
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>> ExecuteAsync(
        this ExecuteRegimeDiscoveryPipelineCommand command,
        IFunctionActorContext<RegimeDiscoveryFunctionActor> context,
        CancellationToken cancellationToken = default)
    {
        var typed = context as IRegimeDiscoveryFunctionContext
            ?? throw new InvalidOperationException(
                $"{nameof(context)} must implement {nameof(IRegimeDiscoveryFunctionContext)}.");
        return await ExecuteAtomicAsync(
            command,
            typed.TimeProvider,
            token => CaptureAndCalculateAsync(command, typed, token),
            (delay, token) => Task.Delay(delay, typed.TimeProvider, token),
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<FunctionResult<
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>> ExecuteAtomicAsync(
        ExecuteRegimeDiscoveryPipelineCommand command,
        TimeProvider timeProvider,
        Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> worker,
        Func<TimeSpan, CancellationToken, Task> timeoutDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(timeoutDelay);

        var now = UtcNow(timeProvider);
        if (now >= command.ExpiresAtUtc)
            return Failed(command, TimeoutOutcome(now));

        using var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var workerTask = worker(workerCancellation.Token);
        var timeoutTask = timeoutDelay(command.ExpiresAtUtc - now, timerCancellation.Token);
        var winner = await Task.WhenAny(workerTask, timeoutTask).ConfigureAwait(false);
        if (winner == timeoutTask)
        {
            workerCancellation.Cancel();
            _ = ObserveLateWorkerAsync(workerTask);
            return Failed(command, TimeoutOutcome(UtcNow(timeProvider)));
        }

        timerCancellation.Cancel();
        var outcome = await workerTask.ConfigureAwait(false);
        now = UtcNow(timeProvider);
        if (now >= command.ExpiresAtUtc)
            return Failed(command, TimeoutOutcome(now));

        return outcome switch
        {
            RegimeDiscoveryExecutionCompleted completed => Completed(command, completed),
            RegimeDiscoveryExecutionFailed failed => Failed(command, failed),
            _ => throw new InvalidOperationException($"Unknown Regime Discovery outcome {outcome.GetType().Name}.")
        };
    }

    static async Task<RegimeDiscoveryExecutionOutcome> CaptureAndCalculateAsync(
        ExecuteRegimeDiscoveryPipelineCommand command,
        IRegimeDiscoveryFunctionContext context,
        CancellationToken cancellationToken)
    {
        var request = RegimeDiscoverySnapshotRequestFactory.Create(
            MarketSeriesIdentity.ForContract(command.TriggerEvent.EntityId.ContractId), command.ParameterSet);
        var snapshotResult = await context.SnapshotProvider.CaptureAsync(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!snapshotResult.IsSuccess || snapshotResult.Snapshot is null)
            return new RegimeDiscoveryExecutionFailed(
                UtcNow(context.TimeProvider),
                "Required Regime Discovery market signals are unavailable.",
                "RegimeDiscoveryCalculation",
                23102,
                snapshotResult.Issues.Select(ToReason).ToArray(),
                Guid.Empty);

        var calculated = await context.CalculationModel.CalculateAsync(
            new RegimeDiscoveryCalculationInput
            {
                ResultId = command.CommandId,
                WorkflowId = command.WorkflowId,
                EntityId = command.WorkflowEntityId,
                TriggerEventId = command.TriggerEvent.Id,
                TriggerEvent = command.TriggerEvent,
                ParameterSet = command.ParameterSet,
                Snapshot = snapshotResult.Snapshot,
                ProducedAtUtc = UtcNow(context.TimeProvider)
            }, context.ExecutionMode, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return calculated.Decision.IsComplete
            ? new RegimeDiscoveryExecutionCompleted(
                calculated,
                snapshotResult.Snapshot.SnapshotId,
                snapshotResult.Snapshot.CacheRevision)
            : new RegimeDiscoveryExecutionFailed(
                calculated.ProducedAtUtc,
                "Regime Discovery specialist or decision calculation did not complete.",
                "RegimeDiscoveryCalculation",
                23102,
                calculated.Reasons,
                snapshotResult.Snapshot.SnapshotId);
    }

    static FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent> Completed(
        ExecuteRegimeDiscoveryPipelineCommand command,
        RegimeDiscoveryExecutionCompleted outcome)
    {
        var payload = MessagePackSerializer.Serialize(outcome.Result);
        var completed = new RegimeDiscoveryPipelineCompletedEvent
        {
            Subject = FunctionSubject(RegimeDiscoveryPipelineCompletedEvent.Verb, command),
            Id = Guid.CreateVersion7(new DateTimeOffset(outcome.Result.ProducedAtUtc, TimeSpan.Zero)),
            EntityId = command.WorkflowEntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = $"{ExecuteRegimeDiscoveryPipelineCommand.Actor}Actor",
            ReceivedOn = outcome.Result.ProducedAtUtc,
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            Result = StrategyStageResultEnvelope.Create(
                outcome.Result.ResultId,
                nameof(RegimeDiscoveryResult),
                RegimeDiscoveryResult.CurrentSchemaVersion,
                payload,
                outcome.Result.MarketDataAsOfUtc,
                outcome.Result.ProducedAtUtc),
            CompletedAtUtc = outcome.Result.ProducedAtUtc,
            ExpiresAtUtc = command.ExpiresAtUtc,
            ParameterPayloadSha256 = command.ParameterPayloadSha256,
            SignalSnapshotId = outcome.SnapshotId
        };
        return FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>
            .Complete(completed);
    }

    static FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent> Failed(
        ExecuteRegimeDiscoveryPipelineCommand command,
        RegimeDiscoveryExecutionFailed outcome)
        => FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>.Fail(
            CreateFailedEvent(command, outcome.ErrorCode, outcome.ErrorMessage, outcome.ErrorType,
                string.Join(',', outcome.Reasons.Select(reason => reason.Code)), outcome.FailedAtUtc));

    internal static RegimeDiscoveryPipelineFailedEvent CreateFailedEvent(
        ExecuteRegimeDiscoveryPipelineCommand command,
        int errorCode,
        string errorMessage,
        string errorType,
        string diagnosticData,
        DateTime failedAtUtc)
        => new()
        {
            Subject = FunctionSubject(RegimeDiscoveryPipelineFailedEvent.Verb, command),
            EntityId = command.WorkflowEntityId,
            Id = Guid.CreateVersion7(new DateTimeOffset(failedAtUtc, TimeSpan.Zero)),
            ErrorDate = failedAtUtc,
            CommandId = command.CommandId,
            EventSource = $"{ExecuteRegimeDiscoveryPipelineCommand.Actor}Actor",
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            ErrorType = ErrorType.Command,
            ErrorData = string.IsNullOrWhiteSpace(diagnosticData) ? errorType : $"{errorType}:{diagnosticData}",
            ReceivedOn = failedAtUtc,
            AggregateId = command.EntityId.Format(),
            CommandName = command.CommandName,
            RouteTo = command.RouteTo.ToString(),
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            ExpiresAtUtc = command.ExpiresAtUtc
        };

    static RegimeDiscoveryExecutionFailed TimeoutOutcome(DateTime now)
        => new(now, "Regime Discovery exceeded its fixed workflow deadline.", "Timeout", 23103,
            [new RegimeDiscoveryReason
            {
                Code = "RegimeDiscoveryExecutionTimedOut",
                Severity = RegimeReasonSeverity.Failure,
                Area = RegimeEvidenceArea.Data
            }], Guid.Empty);

    static ActorSubject FunctionSubject(string verb, ExecuteRegimeDiscoveryPipelineCommand command)
        => new(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor, verb, command.EntityId.Format());

    static DateTime UtcNow(TimeProvider provider) => provider.GetUtcNow().UtcDateTime;

    static async Task ObserveLateWorkerAsync(Task<RegimeDiscoveryExecutionOutcome> workerTask)
    {
        try
        {
            await workerTask.ConfigureAwait(false);
        }
        catch
        {
            // The timeout result is definitive. Observation only prevents a late fault from becoming unobserved.
        }
    }

    static RegimeDiscoveryReason ToReason(RegimeDiscoverySignalObservation observation) => new()
    {
        Code = observation.Availability switch
        {
            RegimeDiscoverySignalAvailability.Stale => RegimeDiscoveryReasonCodes.DataStale,
            RegimeDiscoverySignalAvailability.NotWarm => RegimeDiscoveryReasonCodes.DataNotWarm,
            RegimeDiscoverySignalAvailability.Invalid => RegimeDiscoveryReasonCodes.DataInvalid,
            RegimeDiscoverySignalAvailability.FutureTimestamp => RegimeDiscoveryReasonCodes.FutureDataTimestamp,
            RegimeDiscoverySignalAvailability.SchemaUnsupported => RegimeDiscoveryReasonCodes.DataSchemaUnsupported,
            RegimeDiscoverySignalAvailability.CalculationVersionMismatch =>
                RegimeDiscoveryReasonCodes.CalculationVersionMismatch,
            _ => RegimeDiscoveryReasonCodes.RequiredDataMissing
        },
        Severity = RegimeReasonSeverity.Failure,
        Area = RegimeEvidenceArea.Data,
        TimeFrame = observation.SignalKey.TimeFrame,
        SignalIdentity = observation.SignalIdentity
    };
}

internal abstract record RegimeDiscoveryExecutionOutcome;

internal sealed record RegimeDiscoveryExecutionCompleted(
    RegimeDiscoveryResult Result,
    Guid SnapshotId,
    long SnapshotRevision) : RegimeDiscoveryExecutionOutcome;

internal sealed record RegimeDiscoveryExecutionFailed(
    DateTime FailedAtUtc,
    string ErrorMessage,
    string ErrorType,
    int ErrorCode,
    RegimeDiscoveryReason[] Reasons,
    Guid SnapshotId) : RegimeDiscoveryExecutionOutcome;
