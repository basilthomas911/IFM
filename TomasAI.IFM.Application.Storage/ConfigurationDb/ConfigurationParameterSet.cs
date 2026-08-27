using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Identifies a supported strategy parameter-set table.</summary>
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
    RiskManagement = 6
}

/// <summary>Identifies the guarded lifecycle state of one immutable parameter-set version.</summary>
public enum ConfigurationParameterSetStatus : byte
{
    /// <summary>The version is being authored and cannot be selected.</summary>
    Draft = 0,
    /// <summary>The version is published and eligible during its effective interval.</summary>
    Published = 1,
    /// <summary>The version is retired and no longer eligible for new workflows.</summary>
    Retired = 2
}

/// <summary>Represents one immutable stored parameter-set version and guarded lifecycle metadata.</summary>
public sealed record ConfigurationParameterSet(
    StrategyParameterSetKind Kind,
    Guid ParameterSetId,
    int Version,
    short SchemaVersion,
    ConfigurationParameterSetStatus Status,
    DateTime? EffectiveFromUtc,
    DateTime? RetiredAtUtc,
    string PayloadJson,
    string PayloadSha256,
    string Description,
    DateTime CreatedUtc,
    string CreatedBy);

/// <summary>Represents a resolved, validated Regime Discovery parameter set.</summary>
public sealed record ResolvedRegimeDiscoveryParameterSet(
    RegimeDiscoveryParameterSet ParameterSet,
    string PayloadJson,
    string PayloadSha256,
    DateTime EffectiveFromUtc);
