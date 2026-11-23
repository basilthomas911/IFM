using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

public enum WorkflowEntryCommitPhase { Approved, OutcomeUnknown, Committed, Rejected }

/// <summary>Entry commitment is separate from the five completed decision operators.</summary>
[MessagePackObject]
public sealed record WorkflowEntryCommitState(
    [property: Key(0)] PortfolioEntryApproval Approval,
    [property: Key(1)] WorkflowEntryCommitPhase Phase,
    [property: Key(2)] DateTime UpdatedAtUtc,
    [property: Key(3)] string Reason);
