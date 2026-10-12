namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketConditionVolatilityBehavior : byte
{ Undefined = 0, Contracting = 1, Stable = 2, Expanding = 3, Shock = 4 }
