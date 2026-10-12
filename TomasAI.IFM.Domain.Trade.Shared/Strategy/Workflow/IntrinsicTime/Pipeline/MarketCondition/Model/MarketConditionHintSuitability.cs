namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
/// <summary>Describes an advisory hint without constraining the primary market classification.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketConditionHintSuitability : byte
{ Unknown = 0, Avoid = 1, Eligible = 2, Preferred = 3 }
