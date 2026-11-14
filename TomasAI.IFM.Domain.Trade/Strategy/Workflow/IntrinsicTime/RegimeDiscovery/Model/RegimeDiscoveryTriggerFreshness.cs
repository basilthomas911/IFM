using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

internal static class RegimeDiscoveryTriggerFreshness
{
    internal const string StaleReason = "RD.TRIGGER.STALE";

    internal static IntrinsicTimeStrategyWorkflowView? StopIfStale(
        IntrinsicTimeStrategyWorkflowView started, RegimeDiscoveryExecutionOptions options, DateTime now)
    {
        var trigger = started.TriggerEvent;
        var triggerAt = trigger.CreatedOn != default ? trigger.CreatedOn : trigger.ReceivedOn;
        if (triggerAt == default)
            return null;
        var age = now - triggerAt;
        var limit = options.MaximumTriggerAge(started.EntityId.ItiSignalEntityId.TimePeriod);
        if (age < TimeSpan.FromSeconds(limit))
            return null;

        return started with
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.NoTrade,
            WorkflowRevision = started.WorkflowRevision + 1,
            UpdatedAtUtc = now,
            TerminalAtUtc = now,
            StopReasonCode = StaleReason,
            RegimeDiscovery = started.RegimeDiscovery with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = StrategyWorkflowContinuationDecision.Stop,
                ContinuationReasonCodes = [StaleReason],
                CompletedAtUtc = now,
                SourceEventId = started.TriggerEventId,
                ReasonSentence = string.Create(CultureInfo.InvariantCulture,
                    $"Regime Discovery ended with no trade because the ITI signal was {age.TotalSeconds:0.###} seconds old (limit {limit} seconds).")
            }
        };
    }
}
