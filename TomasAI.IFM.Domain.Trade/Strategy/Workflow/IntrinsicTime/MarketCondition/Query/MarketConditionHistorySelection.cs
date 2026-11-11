using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query;

/// <summary>Qualifies Market Condition history selections before a read model query.</summary>
internal static class MarketConditionHistorySelection
{
    internal static void RequireFund(int fundId, string instrumentRoot, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fundId);
        if (string.IsNullOrWhiteSpace(instrumentRoot))
            throw new ArgumentException("An instrument root is required.", nameof(instrumentRoot));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    internal static void RequireAssessment(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile) || !string.Equals(root, "ES", StringComparison.Ordinal) ||
            !MarketConditionAssessmentParameterSet.IsHorizon(horizon) || pageSize is < 1 or > 100 ||
            !MarketConditionAssessmentContracts.Utc(beforeUtc))
            throw new ArgumentException("Invalid assessment history partition or bound.", nameof(profile));
    }
}
