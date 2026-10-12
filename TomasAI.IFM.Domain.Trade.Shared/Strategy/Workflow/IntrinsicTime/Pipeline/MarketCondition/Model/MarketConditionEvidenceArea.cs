namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketConditionEvidenceArea : byte
{
    Unknown = 0, Workflow = 1, Data = 2, Session = 3, EventRisk = 4, MarketIntegrity = 5,
    FuturesLiquidity = 6, OptionLiquidity = 7, Operations = 8, Classification = 9, Scoring = 10
}
