using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Extensions;

/// <summary>Executes one deadline-bounded Regime Discovery calculation with one outer terminal-state owner.</summary>
public static class ExecuteRegimeDiscoveryPipeline
{
    /// <summary>Runs pure work against the persisted deadline, then commits exactly one winning terminal outcome.</summary>
    public static async Task<ServiceResult<GuidResult>> ExecuteAsync(
        this ExecuteRegimeDiscoveryPipelineCommand command,
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        RegimeDiscoveryCommandState state)
    {
        var typed = context as IRegimeDiscoveryCommandContext
            ?? throw new InvalidOperationException(
                $"{nameof(context)} must implement {nameof(IRegimeDiscoveryCommandContext)}.");
        return await ExecuteAtomicAsync(
            command,
            state,
            typed.TimeProvider,
            cancellationToken => CaptureAndCalculateAsync(command, typed, cancellationToken),
            (delay, cancellationToken) => Task.Delay(delay, typed.TimeProvider, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>Testable outer atomic owner. The worker delegate never receives durable state.</summary>
    internal static async Task<ServiceResult<GuidResult>> ExecuteAtomicAsync(
        ExecuteRegimeDiscoveryPipelineCommand command,
        RegimeDiscoveryCommandState state,
        TimeProvider timeProvider,
        Func<CancellationToken, Task<RegimeDiscoveryExecutionOutcome>> worker,
        Func<TimeSpan, CancellationToken, Task> timeoutDelay)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(timeoutDelay);

        if (state.IsTerminal)
            return state.Matches(command)
                ? new ServiceOk<GuidResult>(new(command.CommandId))
                : new ServiceFailed<GuidResult>(RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                    "A conflicting terminal Regime Discovery input already exists for this workflow execution.",
                    new(command.CommandId));

        var now = UtcNow(timeProvider);
        if (now >= command.ExpiresAtUtc)
            return CommitFailure(command, state, TimeoutOutcome(now));

        using var workerCancellation = new CancellationTokenSource();
        using var timerCancellation = new CancellationTokenSource();
        var workerTask = worker(workerCancellation.Token);
        var timeoutTask = timeoutDelay(command.ExpiresAtUtc - now, timerCancellation.Token);
        var winner = await Task.WhenAny(workerTask, timeoutTask).ConfigureAwait(false);
        if (winner == timeoutTask)
        {
            workerCancellation.Cancel();
            return CommitFailure(command, state, TimeoutOutcome(UtcNow(timeProvider)));
        }

        timerCancellation.Cancel();
        var outcome = await workerTask.ConfigureAwait(false);
        now = UtcNow(timeProvider);
        if (now >= command.ExpiresAtUtc)
        {
            workerCancellation.Cancel();
            return CommitFailure(command, state, TimeoutOutcome(now));
        }

        return outcome switch
        {
            RegimeDiscoveryExecutionCompleted completed => CommitCompleted(command, state, completed),
            RegimeDiscoveryExecutionFailed failed => CommitFailure(command, state, failed),
            _ => throw new InvalidOperationException($"Unknown Regime Discovery outcome {outcome.GetType().Name}.")
        };
    }

    static async Task<RegimeDiscoveryExecutionOutcome> CaptureAndCalculateAsync(
        ExecuteRegimeDiscoveryPipelineCommand command,
        IRegimeDiscoveryCommandContext context,
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
                RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                snapshotResult.Issues.Select(ToReason).ToArray(),
                Guid.Empty);

        var calculated = await context.CalculationModel.CalculateAsync(
            new RegimeDiscoveryCalculationInput
            {
                ResultId = command.CommandId,
                WorkflowId = command.WorkflowId,
                EntityId = command.WorkflowEntityId,
                TriggerEventId = command.TriggerEvent.Id,
                ParameterSet = command.ParameterSet,
                Snapshot = snapshotResult.Snapshot,
                ProducedAtUtc = UtcNow(context.TimeProvider)
            }, context.ExecutionMode, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return calculated.Fusion.IsComplete
            ? new RegimeDiscoveryExecutionCompleted(calculated, snapshotResult.Snapshot.SnapshotId,
                snapshotResult.Snapshot.CacheRevision)
            : new RegimeDiscoveryExecutionFailed(
                calculated.ProducedAtUtc,
                "Regime Discovery specialist or Fusion calculation did not complete.",
                "RegimeDiscoveryCalculation",
                RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                calculated.Reasons,
                snapshotResult.Snapshot.SnapshotId);
    }

    static ServiceResult<GuidResult> CommitCompleted(
        ExecuteRegimeDiscoveryPipelineCommand command,
        RegimeDiscoveryCommandState state,
        RegimeDiscoveryExecutionCompleted outcome)
    {
        var payload = MessagePackSerializer.Serialize(outcome.Result);
        var completed = new RegimeDiscoveryCalculationCompletedEvent
        {
            Subject = EventSubject(RegimeDiscoveryCalculationCompletedEvent.Verb, command),
            Id = Guid.CreateVersion7(new DateTimeOffset(outcome.Result.ProducedAtUtc, TimeSpan.Zero)),
            EntityId = command.WorkflowEntityId,
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            ParameterPayloadSha256 = command.ParameterPayloadSha256,
            SignalSnapshotId = outcome.SnapshotId,
            SignalSnapshotRevision = outcome.SnapshotRevision,
            Result = outcome.Result,
            ResultPayloadSha256 = Convert.ToHexString(SHA256.HashData(payload)),
            CompletedAtUtc = outcome.Result.ProducedAtUtc,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            ExpiresAtUtc = command.ExpiresAtUtc
        };
        return state.Update(completed, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(RegimeDiscoveryCalculationCompletedEvent.ErrorCode,
                "Regime Discovery state rejected the completed transition.", new(command.CommandId));
    }

    static ServiceResult<GuidResult> CommitFailure(
        ExecuteRegimeDiscoveryPipelineCommand command,
        RegimeDiscoveryCommandState state,
        RegimeDiscoveryExecutionFailed outcome)
    {
        var failed = new RegimeDiscoveryCalculationFailedEvent
        {
            Subject = EventSubject(RegimeDiscoveryCalculationFailedEvent.Verb, command),
            Id = Guid.CreateVersion7(new DateTimeOffset(outcome.FailedAtUtc, TimeSpan.Zero)),
            EntityId = command.WorkflowEntityId,
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            ParameterPayloadSha256 = command.ParameterPayloadSha256,
            SignalSnapshotId = outcome.SnapshotId,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = outcome.ErrorCode,
                ErrorMessage = outcome.ErrorMessage,
                ErrorType = outcome.ErrorType,
                ErrorData = string.Join(',', outcome.Reasons.Select(reason => reason.Code)),
                FailedAtUtc = outcome.FailedAtUtc
            },
            Reasons = RegimeDiscoveryMath.OrderReasons(outcome.Reasons),
            FailedAtUtc = outcome.FailedAtUtc,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            ExpiresAtUtc = command.ExpiresAtUtc
        };
        return state.Update(failed, command)
            ? new ServiceFailed<GuidResult>(outcome.ErrorCode, outcome.ErrorMessage, new(command.CommandId))
            : new ServiceFailed<GuidResult>(RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                "Regime Discovery state rejected the failed transition.", new(command.CommandId));
    }

    static RegimeDiscoveryExecutionFailed TimeoutOutcome(DateTime now)
        => new(now, "Regime Discovery exceeded its fixed workflow deadline.", "Timeout",
            RegimeDiscoveryCalculationFailedEvent.TimeoutErrorCode,
            [new RegimeDiscoveryReason
            {
                Code = "RegimeDiscoveryExecutionTimedOut",
                Severity = RegimeReasonSeverity.Failure,
                Area = RegimeEvidenceArea.Data
            }], Guid.Empty);

    static ActorSubject EventSubject(string verb, ExecuteRegimeDiscoveryPipelineCommand command)
        => new(ActorType.Event, RegimeDiscoveryCalculationCompletedEvent.Actor, verb, command.EntityId.Format());

    static DateTime UtcNow(TimeProvider provider) => provider.GetUtcNow().UtcDateTime;

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
