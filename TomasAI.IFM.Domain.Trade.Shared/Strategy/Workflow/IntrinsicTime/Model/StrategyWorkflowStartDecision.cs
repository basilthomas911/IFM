namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Describes the authoritative decision for a strategy workflow start request.</summary>
public enum StrategyWorkflowStartDecision
{
    /// <summary>No start decision has been recorded.</summary>
    None = 0,

    /// <summary>The start request created the active workflow execution.</summary>
    Accepted = 1,

    /// <summary>The start request was rejected because the workflow entity was already active.</summary>
    Rejected = 2
}
