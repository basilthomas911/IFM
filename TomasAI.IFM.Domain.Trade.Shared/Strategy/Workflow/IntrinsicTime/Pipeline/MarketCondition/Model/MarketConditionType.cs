namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketConditionType : byte
{
    Undefined = 0, Directional = 1, RangeBound = 2, Transition = 3,
    VolatilityExpansion = 4, VolatilityContraction = 5, Dislocated = 6, NoOpportunity = 7
}
