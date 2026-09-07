using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

public sealed partial class IntrinsicTimeStrategyWorkflowRealtimeActor
{
    internal static ExecuteMarketConditionAssessmentCommand CreateAssessmentExecute(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started || view.CurrentStage != StrategyWorkflowStage.MarketCondition)
            throw new ArgumentException("Only a committed Market Condition stage can dispatch Assess.");
        var binding = view.AssessmentBinding ?? throw new ArgumentException("Workflow has no frozen assessment profile; start a new assessment workflow.");
        binding.Validate();
        var id = new MarketConditionAssessmentExecutionId(view.EntityId, view.WorkflowId);
        var configuredDeadline = view.UpdatedAtUtc.AddMilliseconds(binding.Parameters.MaximumExecutionMilliseconds);
        var regime = view.RegimeDiscovery.Result ?? throw new ArgumentException("Missing accepted regime.");
        var command = new ExecuteMarketConditionAssessmentCommand
        {
            CommandId = DeterministicPipelineCommandId(view.WorkflowId, view.CurrentStage, view.WorkflowRevision),
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, ExecuteMarketConditionAssessmentCommand.Verb, id.Format()),
            EntityId = id, InputWorkflowRevision = view.WorkflowRevision, WorkflowView = view, TriggerEvent = view.TriggerEvent,
            CorrelationId = view.CorrelationId, CausationId = snapshot.Id, RequestedAtUtc = view.UpdatedAtUtc,
            ExpiresAtUtc = configuredDeadline < view.ExpiresAtUtc ? configuredDeadline : view.ExpiresAtUtc,
            ParameterSet = binding.Parameters, ParameterPayloadSha256 = binding.PayloadSha256,
            RegimeResultEnvelope = regime, RegimePayloadSha256 = regime.PayloadSha256, MarketProfileId = binding.Parameters.MarketProfileId,
            InstrumentRoot = binding.Parameters.InstrumentRoot, TargetHorizon = binding.Parameters.TargetHorizon
        };
        MarketConditionAssessmentContracts.ValidateRequest(command);
        return command;
    }

    static async ValueTask ExecuteAssessmentAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var clock = RequireEventContext(context).TimeProvider;
        var execute = CreateAssessmentExecute(snapshot);
        FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent> terminal;
        try
        {
            var remaining = execute.ExpiresAtUtc - clock.GetUtcNow().UtcDateTime;
            using var deadline = new CancellationTokenSource((remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + FunctionReplyGrace);
            var reply = await context.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand, MarketConditionAssessmentExecutionId,
                FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>(execute, deadline.Token).ConfigureAwait(false);
            terminal = reply.Value ?? throw new InvalidOperationException("Assessment Function returned no terminal result.");
            if (!terminal.IsTerminal) throw new InvalidOperationException("Assessment Function returned an invalid terminal union.");
        }
        catch (Exception ex)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            terminal = FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Fail(new()
            {
                Id = Guid.NewGuid(), CommandId = execute.CommandId, EntityId = execute.WorkflowEntityId, WorkflowId = execute.WorkflowId,
                InputWorkflowRevision = execute.InputWorkflowRevision, CorrelationId = execute.CorrelationId, CausationId = execute.CausationId,
                ErrorCode = MarketConditionAssessmentFailedEvent.ErrorId, ErrorDate = now, ReceivedOn = now,
                ErrorMessage = $"Assessment Function request failed: {ex.GetType().Name}.", ErrorData = "MC.ASSESSMENT.TRANSPORT_FAILED",
                FailureCategory = now >= execute.ExpiresAtUtc ? MarketConditionFailureCategory.Timeout : MarketConditionFailureCategory.CalculationFailed
            });
        }
        if (terminal.IsCompleted)
        {
            var completed = terminal.Completed!;
            var complete = new CompleteMarketConditionCommand
            {
                CommandId = DeterministicTerminalCommandId(completed.EntityId, completed.WorkflowId, completed.InputWorkflowRevision, completed.Id, CompleteMarketConditionCommand.Verb),
                Subject = WorkflowSubject(CompleteMarketConditionCommand.Verb, completed.EntityId), EntityId = completed.EntityId, WorkflowId = completed.WorkflowId,
                InputWorkflowRevision = completed.InputWorkflowRevision, SourceEventId = completed.Id, Result = completed.Result,
                CorrelationId = completed.CorrelationId, CausationId = completed.Id, CompletedAtUtc = completed.CompletedAtUtc
            };
            await context.SendAsync<CompleteMarketConditionCommand, IntrinsicTimeStrategyWorkflowEntityId>(complete, complete.EntityId).ConfigureAwait(false);
        }
        else
        {
            var failed = terminal.Failed!;
            var fail = new FailMarketConditionCommand
            {
                CommandId = DeterministicTerminalCommandId(failed.EntityId, failed.WorkflowId, failed.InputWorkflowRevision, failed.Id, FailMarketConditionCommand.Verb),
                Subject = WorkflowSubject(FailMarketConditionCommand.Verb, failed.EntityId), EntityId = failed.EntityId, WorkflowId = failed.WorkflowId,
                InputWorkflowRevision = failed.InputWorkflowRevision, SourceEventId = failed.Id, FailureCategory = failed.FailureCategory,
                Failure = new() { ErrorCode = failed.ErrorCode, ErrorMessage = failed.ErrorMessage, ErrorType = failed.FailureCategory.ToString(), ErrorData = failed.ErrorData, FailedAtUtc = failed.ErrorDate },
                CorrelationId = failed.CorrelationId, CausationId = failed.Id, FailedAtUtc = failed.ErrorDate
            };
            await context.SendAsync<FailMarketConditionCommand, IntrinsicTimeStrategyWorkflowEntityId>(fail, fail.EntityId).ConfigureAwait(false);
        }
    }
}
