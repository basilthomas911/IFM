using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
/// <summary>One frozen contract in the independently verified Risk calculation, in index points and USD.</summary>
public sealed record RiskLegInput(string InstrumentId, string UnderlyingId, bool IsFuture, bool IsCall, decimal Strike, int SignedRatio, decimal Multiplier, decimal Forward, decimal AnnualRate, decimal Volatility, decimal Years, decimal Delta, decimal Gamma, decimal Vega, decimal Theta);
