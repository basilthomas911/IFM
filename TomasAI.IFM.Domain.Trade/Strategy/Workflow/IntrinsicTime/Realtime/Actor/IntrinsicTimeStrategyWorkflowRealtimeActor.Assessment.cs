using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
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
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

public sealed partial class IntrinsicTimeStrategyWorkflowRealtimeActor
{
    internal static ExecuteMarketConditionAssessmentCommand CreateAssessmentExecute(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started || view.CurrentStage != StrategyWorkflowStage.MarketCondition)
            throw new ArgumentException("Only a committed Market Condition stage can dispatch Assess.",nameof(snapshot));
        var binding = view.AssessmentBinding ?? throw new ArgumentException("Workflow has no frozen assessment profile; start a new assessment workflow.",nameof(snapshot));
        binding.Validate();
        var id = new MarketConditionAssessmentExecutionId(view.EntityId, view.WorkflowId);
        var configuredDeadline = view.UpdatedAtUtc.AddMilliseconds(binding.Parameters.MaximumExecutionMilliseconds);
        if (view.DecisionRuntimeVersion > 0 && Runtime.WorkflowCalculationRuntime.Deadline(view) < configuredDeadline)
            configuredDeadline = Runtime.WorkflowCalculationRuntime.Deadline(view);
        var regime = view.RegimeDiscovery.Result ?? throw new ArgumentException("Missing accepted regime.",nameof(snapshot));
        var command = new ExecuteMarketConditionAssessmentCommand
        {
            CommandId = DeterministicPipelineCommandId(view.WorkflowId, view.CurrentStage, view.WorkflowRevision),
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, ExecuteMarketConditionAssessmentCommand.Verb, id.Format()),
            EntityId = id,
            InputWorkflowRevision = view.WorkflowRevision,
            WorkflowView = view,
            TriggerEvent = view.TriggerEvent,
            CorrelationId = view.CorrelationId,
            CausationId = snapshot.Id,
            RequestedAtUtc = view.UpdatedAtUtc,
            ExpiresAtUtc = configuredDeadline < view.ExpiresAtUtc ? configuredDeadline : view.ExpiresAtUtc,
            ParameterSet = binding.Parameters,
            ParameterPayloadSha256 = binding.PayloadSha256,
            RegimeResultEnvelope = regime,
            RegimePayloadSha256 = regime.PayloadSha256,
            MarketProfileId = binding.Parameters.MarketProfileId,
            InstrumentRoot = binding.Parameters.InstrumentRoot,
            TargetHorizon = binding.Parameters.TargetHorizon,
            CalculationPolicy = DailyFuturesGatePolicy.Matches(view) ? DailyFuturesGatePolicy.Version
                : MarketConditionObservationPolicy.Matches(view) ? MarketConditionObservationPolicy.IsOptionScope(view)
                    ? MarketConditionObservationPolicy.OptionVxVersion : MarketConditionObservationPolicy.Version : null
        };
        if (view.DecisionRuntimeVersion == 0) MarketConditionAssessmentContracts.ValidateRequest(command);
        return command;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A failed Function request is converted to its explicit assessment failure event; the workflow retains a terminal outcome.")]
    internal static async ValueTask ExecuteAssessmentAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot, WorkflowStrategyStateUpdatedEvent? originalSource = null)
    {
        var domain = RequireEventContext(context);
        var source = originalSource ?? snapshot;
        if (await MarketCondition.Realtime.AssessmentFunctionTerminalDelivery.RecoverAsync(domain, source).ConfigureAwait(false)) return;
        var clock = domain.TimeProvider;
        var execute = CreateAssessmentExecute(snapshot);
        // Configuration was frozen by workflow admission. This stage performs
        // no catalog/policy reads and cannot delay the quote with selector setup.
        FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent> terminal;
        try
        {
            if (execute.WorkflowView.DecisionRuntimeVersion > 0 && domain.DecisionRuntime is null)
                throw new InvalidOperationException("MC.RUNTIME.NOT_READY");
            if (execute.WorkflowView.DecisionRuntimeVersion > 0 && domain.DecisionRuntime is { } local)
            {
                var remaining = execute.ExpiresAtUtc - clock.GetUtcNow().UtcDateTime;
                using var deadline = new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
                var provider = domain.PreparedMarketContext ?? throw new InvalidOperationException("MC.INPUT.CACHE_NOT_READY");
                var at = clock.GetUtcNow().UtcDateTime;
                var market = execute.CalculationPolicy == DailyFuturesGatePolicy.Version
                    ? await provider.CaptureDailyFuturesMarketAsync(execute.ParameterSet, at, deadline.Token).ConfigureAwait(false)
                    : execute.CalculationPolicy == MarketConditionObservationPolicy.OptionVxVersion
                        ? await provider.CaptureOptionObservationAsync(execute.ParameterSet, at, deadline.Token).ConfigureAwait(false)
                        : throw new InvalidOperationException("MC.INPUT.POLICY_NOT_PREPARED");
                var calculated = await local.AssessmentAsync(execute, market, deadline.Token).ConfigureAwait(false);
                terminal = MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentCompletedEvent), execute,
                    new MarketConditionExecutionCompleted(calculated, market)), clock);
            }
            else
            {
            var remaining = execute.ExpiresAtUtc - clock.GetUtcNow().UtcDateTime;
            using var deadline = new CancellationTokenSource((remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + FunctionReplyGrace);
            var reply = await context.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand, MarketConditionAssessmentExecutionId,
                FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>(execute, deadline.Token).ConfigureAwait(false);
            terminal = reply.Value ?? throw new InvalidOperationException("Assessment Function returned no terminal result.");
            if (!terminal.IsTerminal) throw new InvalidOperationException("Assessment Function returned an invalid terminal union.");
            }
        }
        catch (Exception ex)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            terminal = MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentFailedEvent), execute,
                new MarketConditionExecutionFailed(now >= execute.ExpiresAtUtc ? MarketConditionFailureCategory.Timeout : MarketConditionFailureCategory.CalculationFailed,
                    "MC.ASSESSMENT.TRANSPORT_FAILED", $"Assessment Function request failed: {ex.GetType().Name}.")), clock);
        }
        if (terminal.IsCompleted)
        {
            var completed = terminal.Completed!;
            if (execute.WorkflowView.DecisionRuntimeVersion > 0) domain.DecisionRuntime?.RecordResult(snapshot.State, completed.Result);
            TradeSelectionPipelineInitialization? selection = null;
            StrategyPipelineFailure? selectionFailure = null;
            var assessment = MarketConditionAssessmentContracts.ReadResult(completed.Result).Assessment;
            if (assessment.Availability != AssessmentAvailability.Unavailable &&
                !assessment.InheritedRestrictions.Contains(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeRestriction.NoNewTrade))
            {
                if (execute.WorkflowView.SelectionBinding is { } frozen) selection = new(frozen);
                else selectionFailure = execute.WorkflowView.SelectionInitializationFailure ?? new StrategyPipelineFailure
                { ErrorCode = 23023, ErrorType = "ConfigurationUnavailable", ErrorMessage = "TS.INIT.ADMISSION_CONFIGURATION_MISSING", FailedAtUtc = clock.GetUtcNow().UtcDateTime };
            }
            var complete = new CompleteMarketConditionCommand
            {
                CommandId = DeterministicTerminalCommandId(completed.EntityId, completed.WorkflowId, completed.InputWorkflowRevision, completed.Id, CompleteMarketConditionCommand.Verb),
                Subject = WorkflowSubject(CompleteMarketConditionCommand.Verb, completed.EntityId),
                EntityId = completed.EntityId,
                WorkflowId = completed.WorkflowId,
                InputWorkflowRevision = completed.InputWorkflowRevision,
                SourceEventId = completed.Id,
                Result = completed.Result,
                CorrelationId = completed.CorrelationId,
                CausationId = completed.Id,
                CompletedAtUtc = completed.CompletedAtUtc,
                AssessmentBinding = execute.WorkflowView.AssessmentBinding,
                SelectionBinding = selection?.Binding,
                FundId = 0,
                TradeSelectionInitializationFailure = selectionFailure
            };
            await WorkflowPublicationDelivery.PublishAsync(domain, source, complete).ConfigureAwait(false);
        }
        else
        {
            var failed = terminal.Failed!;
            var fail = new FailMarketConditionCommand
            {
                CommandId = DeterministicTerminalCommandId(failed.EntityId, failed.WorkflowId, failed.InputWorkflowRevision, failed.Id, FailMarketConditionCommand.Verb),
                Subject = WorkflowSubject(FailMarketConditionCommand.Verb, failed.EntityId),
                EntityId = failed.EntityId,
                WorkflowId = failed.WorkflowId,
                InputWorkflowRevision = failed.InputWorkflowRevision,
                SourceEventId = failed.Id,
                FailureCategory = failed.FailureCategory,
                Failure = new() { ErrorCode = failed.ErrorCode, ErrorMessage = failed.ErrorMessage, ErrorType = failed.FailureCategory.ToString(), ErrorData = failed.ErrorData, FailedAtUtc = failed.ErrorDate },
                CorrelationId = failed.CorrelationId,
                CausationId = failed.Id,
                FailedAtUtc = failed.ErrorDate
            };
            await MarketCondition.Realtime.AssessmentFunctionTerminalDelivery.CaptureAsync(domain, source, fail).ConfigureAwait(false);
        }
    }
}
