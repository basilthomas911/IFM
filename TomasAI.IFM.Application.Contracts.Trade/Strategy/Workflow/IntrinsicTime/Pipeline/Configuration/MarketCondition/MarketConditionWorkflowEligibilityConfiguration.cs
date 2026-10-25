using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionWorkflowEligibilityConfiguration
{
    RegimeRestriction[]? _blockingRegimeRestrictions = [RegimeRestriction.NoNewTrade];

    [Key(0)] public int MaximumRegimeAgeSeconds { get; init; } = 120;
    [Key(1)] public int MaximumTriggerAgeSeconds { get; init; } = 30;
    [Key(2)] public bool RequireEntriesEnabled { get; init; } = true;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(3)]
    public RegimeRestriction[] BlockingRegimeRestrictions
    {
        get => _blockingRegimeRestrictions is null ? null! : [.. _blockingRegimeRestrictions];
        init => _blockingRegimeRestrictions = value is null ? null : [.. value.Order()];
    }
}
