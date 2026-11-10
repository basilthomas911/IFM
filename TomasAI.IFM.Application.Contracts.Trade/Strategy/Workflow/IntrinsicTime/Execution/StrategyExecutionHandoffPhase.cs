namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Durable progress toward the original opening execution; unknown outcomes retain the current phase.</summary>
public enum StrategyExecutionHandoffPhase
{
    Accepted,
    FinancialAdmitted,
    OrderCreated,
    OrderApproved,
    OrderReady,
    ExecutionLinked,
    Opened,
    Rejected,
    Cancelled,
    Expired
}
