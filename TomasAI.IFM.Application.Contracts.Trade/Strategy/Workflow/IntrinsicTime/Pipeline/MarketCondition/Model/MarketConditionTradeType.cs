namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
/// <summary>Identifies the bounded trade families understood as advisory downstream consumers.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketConditionTradeType : byte
{ Unknown = 0, Futures = 1, VerticalSpread = 2, IronCondor = 3 }
