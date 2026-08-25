namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Identifies a stage in the Intrinsic Time Strategy workflow pipeline.</summary>
public enum StrategyWorkflowStage
{
    /// <summary>No stage is active.</summary>
    None = 0,

    /// <summary>Discovers the current market regime.</summary>
    RegimeDiscovery = 1,

    /// <summary>Evaluates the market condition within the accepted regime.</summary>
    MarketCondition = 2,

    /// <summary>Selects a candidate trade strategy.</summary>
    TradeSelection = 3,

    /// <summary>Composes the proposed order structure.</summary>
    OrderComposition = 4,

    /// <summary>Applies final workflow risk management.</summary>
    RiskManagement = 5
}
