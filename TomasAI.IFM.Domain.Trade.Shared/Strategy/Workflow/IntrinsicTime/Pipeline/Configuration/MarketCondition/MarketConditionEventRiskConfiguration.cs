using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionEventRiskConfiguration
{
    string[]? _requiredEventCategories = ["HighImpact", "RateDecision"];

    [Key(0)] public int HighImpactBeforeMinutes { get; init; } = 15;
    [Key(1)] public int HighImpactAfterMinutes { get; init; } = 10;
    [Key(2)] public int RateDecisionBeforeMinutes { get; init; } = 30;
    [Key(3)] public int RateDecisionAfterMinutes { get; init; } = 20;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(4)]
    public string[] RequiredEventCategories
    {
        get => _requiredEventCategories is null ? null! : [.. _requiredEventCategories];
        init => _requiredEventCategories = value is null
            ? null
            : [.. value.Order(StringComparer.Ordinal)];
    }
}
