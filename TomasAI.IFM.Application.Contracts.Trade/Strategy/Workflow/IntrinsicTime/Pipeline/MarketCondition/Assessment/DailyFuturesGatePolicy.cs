using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

/// <summary>Versioned entry permission for Daily futures; supplies no threshold, entry or exit prices.</summary>
public static class DailyFuturesGatePolicy
{
    public const string LegacyVersion = "DailyFuturesGate.v1";
    public const string PreviousVersion = "DailyFuturesGate.v2";
    public const string Version = "DailyFuturesGate.v3";
    public static bool IsSupported(string? policy) => policy is Version or PreviousVersion or LegacyVersion;
    public const string Prefix = "MC.DAILY_FUTURES.";
    public static bool Matches(IntrinsicTimeStrategyWorkflowView view) =>
        view.EntityId.Assignment?.Family == "Future" && view.TriggerEvent.EntityId.TimePeriod == TimeFrameType.Daily
        && view.RegimeDiscovery.Result?.RegimeResult is { } regime && EmaStackRegimePolicy.Applies(regime);
    public static bool Applies(IntrinsicTimeStrategyWorkflowEntityId entity, HorizonAssessment assessment) =>
        entity.Assignment?.Family == "Future" && assessment.Horizon == TimeFrameType.Daily
        && assessment.EvidenceItems.Any(e => IsSupported(e.SourceId) && e.Feature == "Policy" && e.Reason == e.SourceId);
}
