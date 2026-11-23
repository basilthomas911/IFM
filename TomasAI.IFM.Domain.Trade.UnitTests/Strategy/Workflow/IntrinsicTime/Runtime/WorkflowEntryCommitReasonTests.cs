using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed class WorkflowEntryCommitReasonTests
{
    [Theory]
    [InlineData(WorkflowEntryCommitPhase.Approved, "entry commitment is pending")]
    [InlineData(WorkflowEntryCommitPhase.OutcomeUnknown, "outcome is unconfirmed")]
    [InlineData(WorkflowEntryCommitPhase.Committed, "Entry commitment confirmed")]
    [InlineData(WorkflowEntryCommitPhase.Rejected, "No entry was committed")]
    public void Saved_reasons_distinguish_decision_approval_from_financial_commitment(WorkflowEntryCommitPhase phase, string expected)
    {
        var view = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification().State with
        {
            CurrentStage = StrategyWorkflowStage.RiskManagement,
            RiskManagement = new() { ProcessingStatus = StrategyActorProcessingStatus.Completed },
            EntryCommit = new(new PortfolioEntryApproval(), phase, DateTime.UtcNow, "ENTRY.TEST.REASON")
        };
        StrategyWorkflowStageReasons.Record(view).RiskManagement.ReasonSentence.Should().Contain(expected);
        if (phase == WorkflowEntryCommitPhase.OutcomeUnknown)
            StrategyWorkflowStageReasons.Record(view).RiskManagement.ReasonSentence.Should().Contain("will not be rerun");
    }
}
