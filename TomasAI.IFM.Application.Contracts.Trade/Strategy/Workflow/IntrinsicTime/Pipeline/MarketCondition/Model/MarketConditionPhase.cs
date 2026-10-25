namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketConditionPhase : byte
{
    Undefined = 0, Initiating = 1, Confirmed = 2, Continuing = 3,
    Weakening = 4, Exhausting = 5, Reversing = 6
}
