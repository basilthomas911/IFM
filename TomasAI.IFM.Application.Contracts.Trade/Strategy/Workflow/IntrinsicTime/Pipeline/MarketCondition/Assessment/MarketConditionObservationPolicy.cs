using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

/// <summary>Available means the required observations are qualified, not that a strategy may enter.</summary>
public static class MarketConditionObservationPolicy
{
    public const string Version = "MarketObservation.v1";
    public const string OptionVxVersion = "OptionVxBackwardation.v1";
    public const string BackwardationReason = "MC.OPTIONS.VX_BACKWARDATION";
    public static bool IsOptionScope(IntrinsicTimeStrategyWorkflowView view) =>
        view.EntityId.Assignment is { } assignment && assignment.Timeframe == view.TriggerEvent.EntityId.TimePeriod
        && (assignment.Family, view.TriggerEvent.EntityId.TimePeriod) is
        ("IronCondor", TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly) or
        ("Vertical", TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly) or
        ("VerticalSpread", TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly);
    public static bool HasOptionVeto(IntrinsicTimeStrategyWorkflowView view, HorizonAssessment assessment) =>
        IsOptionScope(view) && assessment.EvidenceItems.Any(e => e.Feature == "Policy" && e.SourceId == OptionVxVersion && e.Reason == OptionVxVersion)
        && assessment.LimitationReasons.Contains(BackwardationReason, StringComparer.Ordinal)
        && assessment.EvidenceItems.Any(e => e.SourceId == "VX.TermStructure" && e.Feature == "FrontSecondRatio" && e.Value > 1);
    public static bool Matches(IntrinsicTimeStrategyWorkflowView view) =>
        view.RegimeDiscovery.Result?.RegimeResult is { } regime && EmaStackRegimePolicy.Applies(regime);
    public static bool Applies(HorizonAssessment assessment) => assessment.EvidenceItems.Any(e =>
        e.Feature == "Policy" && e.Reason == e.SourceId && e.SourceId is Version or OptionVxVersion or DailyFuturesGatePolicy.Version);
}
