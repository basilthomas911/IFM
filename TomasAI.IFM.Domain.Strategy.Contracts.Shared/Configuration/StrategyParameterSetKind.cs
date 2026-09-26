namespace TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;

/// <summary>
/// Identifies a supported strategy parameter-set family.
/// </summary>
public enum StrategyParameterSetKind : byte
{
    /// <summary>Intrinsic Time Strategy Workflow parameters.</summary>
    IntrinsicTimeStrategyWorkflow = 1,

    /// <summary>Regime Discovery parameters.</summary>
    RegimeDiscovery = 2,

    /// <summary>Market Condition parameters.</summary>
    MarketCondition = 3,

    /// <summary>Trade Selection parameters.</summary>
    TradeSelection = 4,

    /// <summary>Order Composition parameters.</summary>
    OrderComposition = 5,

    /// <summary>Risk Management parameters.</summary>
    RiskManagement = 6,

    /// <summary>Market Condition Assessment parameters.</summary>
    MarketConditionAssessment = 7
}
