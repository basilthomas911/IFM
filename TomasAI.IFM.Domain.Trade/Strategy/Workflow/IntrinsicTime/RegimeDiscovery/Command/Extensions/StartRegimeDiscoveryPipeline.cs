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

/// <summary>Executes the asynchronous Regime Discovery pipeline start command.</summary>
public static class StartRegimeDiscoveryPipeline
{
    /// <summary>Captures one snapshot, calculates one terminal outcome, and updates private state.</summary>
    /// <param name="command">Start command sent by Strategy Workflow.</param>
    /// <param name="context">Closed-generic Regime Discovery Command context.</param>
    /// <param name="state">Loaded private event-sourced state.</param>
    /// <returns>The command acknowledgement carrying the original command identity.</returns>
    public static async Task<ServiceResult<GuidResult>> ExecuteAsync(
        this StartRegimeDiscoveryPipelineCommand command,
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        RegimeDiscoveryCommandState state)
    {
        var typedContext = context as IRegimeDiscoveryCommandContext
            ?? throw new InvalidOperationException(
                $"{nameof(context)} must implement {nameof(IRegimeDiscoveryCommandContext)}.");
        if (state.IsTerminal)
            return state.Matches(command)
                ? new ServiceOk<GuidResult>(new(command.CommandId))
                : new ServiceFailed<GuidResult>(RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                    "A conflicting terminal Regime Discovery input already exists for this workflow entity.",
                    new(command.CommandId));

        var request = RegimeDiscoverySnapshotRequestFactory.Create(
            MarketSeriesIdentity.ForContract(command.TriggerEvent.EntityId.ContractId), command.ParameterSet);
        var snapshotResult = await typedContext.SnapshotProvider.CaptureAsync(request).ConfigureAwait(false);
        if (!snapshotResult.IsSuccess || snapshotResult.Snapshot is null)
            return Fail(command, state, typedContext.TimeProvider.GetUtcNow().UtcDateTime,
                "Required Regime Discovery market signals are unavailable.",
                snapshotResult.Issues.Select(ToReason).ToArray(), Guid.Empty);

        var calculated = await typedContext.CalculationModel.CalculateAsync(
            new RegimeDiscoveryCalculationInput
            {
                ResultId = command.CommandId,
                WorkflowId = command.WorkflowId,
                EntityId = command.EntityId,
                TriggerEventId = command.TriggerEvent.Id,
                ParameterSet = command.ParameterSet,
                Snapshot = snapshotResult.Snapshot,
                ProducedAtUtc = typedContext.TimeProvider.GetUtcNow().UtcDateTime
            }, typedContext.ExecutionMode).ConfigureAwait(false);
        if (!calculated.Fusion.IsComplete)
            return Fail(command, state, calculated.ProducedAtUtc,
                "Regime Discovery specialist or Fusion calculation did not complete.",
                calculated.Reasons, snapshotResult.Snapshot.SnapshotId);

        var resultPayload = MessagePackSerializer.Serialize(calculated);
        var completed = new RegimeDiscoveryCalculationCompletedEvent
        {
            Subject = new(ActorType.Event, RegimeDiscoveryCalculationCompletedEvent.Actor,
                RegimeDiscoveryCalculationCompletedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(typedContext.TimeProvider.GetUtcNow()),
            EntityId = command.EntityId,
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            ParameterPayloadSha256 = command.ParameterPayloadSha256,
            SignalSnapshotId = snapshotResult.Snapshot.SnapshotId,
            SignalSnapshotRevision = snapshotResult.Snapshot.CacheRevision,
            Result = calculated,
            ResultPayloadSha256 = Convert.ToHexString(SHA256.HashData(resultPayload)),
            CompletedAtUtc = calculated.ProducedAtUtc,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId
        };
        return state.Update(completed, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(RegimeDiscoveryCalculationCompletedEvent.ErrorCode,
                "Regime Discovery state rejected the completed transition.", new(command.CommandId));
    }

    static ServiceResult<GuidResult> Fail(
        StartRegimeDiscoveryPipelineCommand command,
        RegimeDiscoveryCommandState state,
        DateTime failedAtUtc,
        string errorMessage,
        RegimeDiscoveryReason[] reasons,
        Guid snapshotId)
    {
        var failed = new RegimeDiscoveryCalculationFailedEvent
        {
            Subject = new(ActorType.Event, RegimeDiscoveryCalculationFailedEvent.Actor,
                RegimeDiscoveryCalculationFailedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(failedAtUtc, TimeSpan.Zero)),
            EntityId = command.EntityId,
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            ParameterPayloadSha256 = command.ParameterPayloadSha256,
            SignalSnapshotId = snapshotId,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                ErrorMessage = errorMessage,
                ErrorType = "RegimeDiscoveryCalculation",
                ErrorData = string.Join(',', reasons.Select(reason => reason.Code)),
                FailedAtUtc = failedAtUtc
            },
            Reasons = RegimeDiscoveryMath.OrderReasons(reasons),
            FailedAtUtc = failedAtUtc,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId
        };
        return state.Update(failed, command)
            ? new ServiceFailed<GuidResult>(RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                errorMessage, new(command.CommandId))
            : new ServiceFailed<GuidResult>(RegimeDiscoveryCalculationFailedEvent.ErrorCode,
                "Regime Discovery state rejected the failed transition.", new(command.CommandId));
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
