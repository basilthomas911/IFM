using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;

/// <summary>Builds failed Regime Discovery events through the Function event map.</summary>
public static class FailRegimeDiscoveryPipeline
{
    /// <summary>Builds the mapped failure for an outcome, conflict, or lifecycle exception.</summary>
    /// <param name="input">The decoded request and failure details; the request may be null after parsing fails.</param>
    /// <param name="timeProvider">The clock used for conflict and lifecycle failure timestamps.</param>
    /// <returns>A non-durable failure response carrying workflow identity and diagnostic information.</returns>
    public static FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent> Fail(
        this FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand> input, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var command = input.Request;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        RegimeDiscoveryPipelineFailedEvent failed;
        if (command is null)
        {
            failed = new RegimeDiscoveryPipelineFailedEvent
            {
                Subject = new ActorSubject(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor,
                    RegimeDiscoveryPipelineFailedEvent.Verb, string.Empty),
                Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
                ErrorDate = now,
                ReceivedOn = now,
                ErrorCode = RegimeDiscoveryPipelineFailedEvent.ErrorId,
                ErrorMessage = "Regime Discovery Function request could not be processed.",
                ErrorType = ErrorType.Command,
                ErrorData = input.Stage.ToString(),
                EventSource = $"{ExecuteRegimeDiscoveryPipelineCommand.Actor}Actor",
                CommandName = nameof(ExecuteRegimeDiscoveryPipelineCommand),
                PipelineStage = StrategyWorkflowStage.RegimeDiscovery
            };
        }
        else if (input.IsConflict)
            failed = CreateFailedEvent(command, RegimeDiscoveryPipelineFailedEvent.ErrorId,
                "A conflicting completed Regime Discovery input already exists for this execution.",
                "FunctionConflict", string.Empty, now);
        else if (input.Outcome is RegimeDiscoveryExecutionFailed outcome)
            failed = CreateFailedEvent(command, outcome.ErrorCode, outcome.ErrorMessage, outcome.ErrorType,
                string.IsNullOrWhiteSpace(outcome.DiagnosticData)
                    ? string.Join(',', outcome.Reasons.Select(reason => reason.Code)) : outcome.DiagnosticData,
                outcome.FailedAtUtc);
        else if (input.Exception is { } exception)
            failed = CreateFailedEvent(command, command.ErrorCode,
                input.Stage == FunctionFailureStage.Projection
                    ? "Regime Discovery result projection failed."
                    : input.Stage == FunctionFailureStage.Persistence
                        ? "Regime Discovery completed state could not be persisted."
                        : "Regime Discovery Function execution failed.",
                input.Stage.ToString(), exception.GetType().Name, now);
        else
            throw new InvalidOperationException("Failure requires a failed outcome, conflict, or lifecycle exception.");
        return FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>.Fail(failed);
    }

    /// <summary>Builds a failure event carrying the originating execution's routing and workflow metadata.</summary>
    /// <param name="command">The request associated with the failed Function execution.</param>
    /// <param name="errorCode">The application error code identifying the failure.</param>
    /// <param name="errorMessage">The human-readable explanation of the failure.</param>
    /// <param name="errorType">The diagnostic category stored in <c>ErrorData</c>; the event's <c>ErrorType</c> is Command.</param>
    /// <param name="diagnosticData">Optional diagnostic detail appended to the category when nonblank.</param>
    /// <param name="failedAtUtc">The UTC failure time used for timestamps and the version-7 event identifier.</param>
    /// <returns>A failure event for the Function reply, without publishing or persisting it.</returns>
    static RegimeDiscoveryPipelineFailedEvent CreateFailedEvent(
        ExecuteRegimeDiscoveryPipelineCommand command,
        int errorCode,
        string errorMessage,
        string errorType,
        string diagnosticData,
        DateTime failedAtUtc)
        => new()
        {
            Subject = new ActorSubject(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor,
                RegimeDiscoveryPipelineFailedEvent.Verb, command.EntityId.Format()),
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

}
