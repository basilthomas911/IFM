using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionSessionConfiguration
{
    DayOfWeek[]? _eligibleWeekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    [Key(0)] public string ExchangeTimeZoneId { get; init; } = "America/New_York";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(1)]
    public DayOfWeek[] EligibleWeekdays
    {
        get => _eligibleWeekdays is null ? null! : [.. _eligibleWeekdays];
        init => _eligibleWeekdays = value is null ? null : [.. value.Order()];
    }
    [Key(2)] public TimeSpan EntryWindowStart { get; init; } = new(9, 35, 0);
    [Key(3)] public TimeSpan EntryWindowEnd { get; init; } = new(15, 30, 0);
    [Key(4)] public bool RequireOpenExchangeState { get; init; } = true;
}
