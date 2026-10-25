using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionOperationalReadinessConfiguration
{
    string[]? _requiredHealthSources =
        ["FuturesOptionFeed", "IbkrSession", "LatestValueCache", "PrimaryFuturesFeed"];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(0)]
    public string[] RequiredHealthSources
    {
        get => _requiredHealthSources is null ? null! : [.. _requiredHealthSources];
        init => _requiredHealthSources = value is null
            ? null
            : [.. value.Order(StringComparer.Ordinal)];
    }
    [Key(1)] public bool TreatReportedDegradedAsBlocked { get; init; } = true;
}
