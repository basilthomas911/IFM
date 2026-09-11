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
    /// <param name="cancellationToken">Caller/deadline cancellation supplied by the base and forwarded to capture and calculation.</param>
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
        cancellationToken.ThrowIfCancellationRequested();
        var outcome = await CaptureAndCalculateAsync(command, context, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome switch
        {
            RegimeDiscoveryExecutionCompleted completed => dispatchEvent(new(typeof(RegimeDiscoveryPipelineCompletedEvent), command, completed)),
            RegimeDiscoveryExecutionFailed failed => dispatchEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent), command, failed)),
            _ => throw new InvalidOperationException($"Unknown Regime Discovery outcome {outcome.GetType().Name}.")
        };
    }

    /// <summary>Evaluates the immutable signal snapshot qualified by pipeline initialization.</summary>
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
        var snapshot = command.Snapshot;
        if (snapshot.SnapshotId == Guid.Empty || snapshot.Observations.Length == 0)
            return new RegimeDiscoveryExecutionFailed(
                UtcNow(context.TimeProvider),
                "Regime Discovery was invoked without an initialized market-signal snapshot.",
                "RegimeDiscoveryInitialization",
                23102,
                [new RegimeDiscoveryReason
                {
                    Code = RegimeDiscoveryReasonCodes.RequiredDataMissing,
                    Severity = RegimeReasonSeverity.Failure,
                    Area = RegimeEvidenceArea.Data
                }],
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
                Snapshot = snapshot,
                ProducedAtUtc = UtcNow(context.TimeProvider)
            }, context.ExecutionMode, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return calculated.Decision.IsComplete
            ? new RegimeDiscoveryExecutionCompleted(
                calculated,
                snapshot.SnapshotId,
                snapshot.CacheRevision)
            : new RegimeDiscoveryExecutionFailed(
                calculated.ProducedAtUtc,
                "Regime Discovery specialist or decision calculation did not complete.",
                "RegimeDiscoveryCalculation",
                23102,
                calculated.Reasons,
                snapshot.SnapshotId,
                FormatReasons(calculated.Reasons));
    }

    static string FormatReasons(IEnumerable<RegimeDiscoveryReason> reasons) => string.Join(';', reasons
        .Select(reason => $"Code={reason.Code},Area={reason.Area},TimeFrame={reason.TimeFrame},SignalIdentity={reason.SignalIdentity}"));

    /// <summary>Reads the domain clock when stamping captured evidence and calculated outcomes.</summary>
    static DateTime UtcNow(TimeProvider provider) => provider.GetUtcNow().UtcDateTime;

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
