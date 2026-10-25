using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;

/// <summary>
/// Represents a resolved Trade Selection parameter-set version and its lifecycle state.
/// </summary>
public sealed record ResolvedTradeSelectionParameterSet(
    TradeSelectionParameterSet ParameterSet,
    string PayloadSha256,
    ConfigurationParameterSetStatus Status,
    DateTime? EffectiveFromUtc,
    DateTime? RetiredAtUtc);
