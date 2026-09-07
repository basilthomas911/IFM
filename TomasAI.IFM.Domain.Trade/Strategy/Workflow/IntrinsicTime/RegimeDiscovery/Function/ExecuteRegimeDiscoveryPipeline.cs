using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;

/// <summary>Produces one completed candidate or one non-durable failed Function response before the deadline.</summary>
public static class ExecuteRegimeDiscoveryPipeline
{
    /// <summary>Captures market signals and calculates one Regime Discovery result within the request deadline.</summary>
    /// <param name="command">The validated execution request, including frozen parameters and workflow identity.</param>
    /// <param name="context">The Function context providing the clock, snapshot provider, and calculation model.</param>
    /// <param name="dispatchEvent">The actor callback that resolves its exact-type terminal event map.</param>
    /// <param name="cancellationToken">Cancellation forwarded to the calculation worker and deadline timer.</param>
    /// <returns>A completed candidate or failed response for the owning Function actor to handle.</returns>
    /// <exception cref="ArgumentNullException">The domain context is null.</exception>
    /// <remarks>This method does not project, persist, or publish the result; those responsibilities remain with the actor lifecycle.</remarks>
    public static async ValueTask<FunctionResult<
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>> ExecuteAsync(
        this ExecuteRegimeDiscoveryPipelineCommand command,
        IRegimeDiscoveryFunctionContext context,
        Func<FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand>,
            FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>> dispatchEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await ExecuteAtomicAsync(
            command,
            context.TimeProvider,
            token => CaptureAndCalculateAsync(command, context, token),
            (delay, token) => Task.Delay(delay, context.TimeProvider, token),
            dispatchEvent,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resolves a calculation worker and its deadline timer into one terminal Function response.</summary>
    /// <param name="command">The execution request containing the fixed UTC deadline and response metadata.</param>
    /// <param name="timeProvider">The clock used to check expiry before work starts and after work completes.</param>
    /// <param name="worker">The asynchronous calculation invoked with a linked cancellation token.</param>
    /// <param name="timeoutDelay">The timer factory for the remaining duration and its linked cancellation token.</param>
    /// <param name="dispatchEvent">The actor callback that resolves its exact-type terminal event map.</param>
    /// <param name="cancellationToken">Caller cancellation linked to both the worker and the timer.</param>
    /// <returns>The mapped worker outcome, or a timeout failure when the timer wins or the deadline has been reached.</returns>
    /// <exception cref="ArgumentNullException">A required command, clock, worker, or timer factory is null.</exception>
    /// <exception cref="InvalidOperationException">The worker returns an unsupported outcome type.</exception>
    /// <remarks>
    /// Expiry at the exact deadline takes precedence over a completed calculation. When the timer wins,
    /// the worker is cancelled and observed without delaying the timeout response. When the worker wins,
    /// its exception or cancellation propagates to the caller. Atomicity refers to selecting a terminal
    /// response, not to a database transaction.
    /// </remarks>
    internal static async Task<FunctionResult<
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>> ExecuteAtomicAsync(
        ExecuteRegimeDiscoveryPipelineCommand command,
        TimeProvider timeProvider,
        Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> worker,
        Func<TimeSpan, CancellationToken, Task> timeoutDelay,
        Func<FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand>,
            FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>> dispatchEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(timeoutDelay);
        ArgumentNullException.ThrowIfNull(dispatchEvent);

        var now = UtcNow(timeProvider);
        if (now >= command.ExpiresAtUtc)
            return dispatchEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent), command, TimeoutOutcome(now)));

        using var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var workerTask = worker(workerCancellation.Token);
        var timeoutTask = timeoutDelay(command.ExpiresAtUtc - now, timerCancellation.Token);
        var winner = await Task.WhenAny(workerTask, timeoutTask).ConfigureAwait(false);
        if (winner == timeoutTask)
        {
            workerCancellation.Cancel();
            _ = ObserveLateWorkerAsync(workerTask);
            return dispatchEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent), command, TimeoutOutcome(UtcNow(timeProvider))));
        }

        timerCancellation.Cancel();
        var outcome = await workerTask.ConfigureAwait(false);
        now = UtcNow(timeProvider);
        if (now >= command.ExpiresAtUtc)
            return dispatchEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent), command, TimeoutOutcome(now)));

        return outcome switch
        {
            RegimeDiscoveryExecutionCompleted completed => dispatchEvent(new(typeof(RegimeDiscoveryPipelineCompletedEvent), command, completed)),
            RegimeDiscoveryExecutionFailed failed => dispatchEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent), command, failed)),
            _ => throw new InvalidOperationException($"Unknown Regime Discovery outcome {outcome.GetType().Name}.")
        };
    }

    /// <summary>Captures the configured signal snapshot and evaluates it with the Regime Discovery calculation model.</summary>
    /// <param name="command">The validated request supplying the trigger contract, parameters, and result identity.</param>
    /// <param name="context">The snapshot provider, calculation model, execution mode, and clock for this execution.</param>
    /// <param name="cancellationToken">Cancellation passed to both operations and checked after each completes.</param>
    /// <returns>A completed outcome with snapshot provenance, or a failure describing unavailable data or an incomplete decision.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is observed during capture or calculation.</exception>
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

    /// <summary>Creates the standard fixed-deadline timeout outcome with error code 23103.</summary>
    /// <param name="now">The UTC time at which expiry was observed.</param>
    /// <returns>A timeout failure with a data-area reason and an empty snapshot identifier.</returns>
    static RegimeDiscoveryExecutionFailed TimeoutOutcome(DateTime now)
        => new(now, "Regime Discovery exceeded its fixed workflow deadline.", "Timeout", 23103,
            [new RegimeDiscoveryReason
            {
                Code = "RegimeDiscoveryExecutionTimedOut",
                Severity = RegimeReasonSeverity.Failure,
                Area = RegimeEvidenceArea.Data
            }], Guid.Empty);

    /// <summary>Reads the supplied clock as a UTC <see cref="DateTime"/>.</summary>
    /// <param name="provider">The execution clock, which may be substituted for deterministic tests.</param>
    /// <returns>The clock's current time with <see cref="DateTimeKind.Utc"/>.</returns>
    static DateTime UtcNow(TimeProvider provider) => provider.GetUtcNow().UtcDateTime;

    /// <summary>Observes a worker that continues after the timeout response has been selected.</summary>
    /// <param name="workerTask">The outstanding calculation task whose eventual completion must be observed.</param>
    /// <returns>A task that completes after the worker settles, suppressing its cancellation or exception.</returns>
    /// <remarks>The worker's late outcome is discarded and cannot replace the already selected timeout response.</remarks>
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

    /// <summary>Maps a snapshot availability issue to a stable Regime Discovery data-failure reason.</summary>
    /// <param name="observation">The signal observation containing availability, timeframe, and signal identity.</param>
    /// <returns>A failure-severity data reason; unlisted availability values map to required data missing.</returns>
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
    Guid SnapshotId,
    string DiagnosticData = "") : RegimeDiscoveryExecutionOutcome;
