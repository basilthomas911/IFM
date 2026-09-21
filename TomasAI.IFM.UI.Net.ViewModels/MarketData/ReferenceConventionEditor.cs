using System.ComponentModel;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>User-reviewed fields only; the provider identity and exact strike are never editable here.</summary>
public sealed class ReferenceConventionEditor
{
    [Category("Review")] public ReferenceReviewState ReviewState { get; set; } = ReferenceReviewState.Draft;
    [Category("Review")] public string MappingVersion { get; set; } = "";
    [Category("Review")] public string EvidenceId { get; set; } = "";
    [Category("Review")] public DateTimeOffset? EffectiveFromUtc { get; set; }
    [Category("Review")] public DateTimeOffset? EffectiveUntilUtc { get; set; }
    [Category("Calendar")] public string ExchangeTimeZoneId { get; set; } = "";
    [Category("Calendar")] public string CalendarVersion { get; set; } = "";
    [Category("Calendar")] public DateTimeOffset? LastTradingUtc { get; set; }
    [Category("Calendar")] public DateTimeOffset? ExerciseCutoffUtc { get; set; }
    [Category("Economics")] public ReferenceExerciseStyle ExerciseStyle { get; set; }
    [Category("Economics")] public ReferenceSettlementStyle SettlementStyle { get; set; }
    [Category("Economics")] public ReferencePremiumStyle PremiumStyle { get; set; }
    [Category("Economics")] public ReferencePremiumTickRule PremiumTickRule { get; set; }
    [Category("Economics")] public string TickRuleVersion { get; set; } = "";
    [Category("Economics")] public ReferenceDayCount DayCount { get; set; }
    [Category("Underlying")] public string UnderlyingContractId { get; set; } = "";
    [Category("Underlying")] public string ExerciseResultContractId { get; set; } = "";

    public FuturesContractV3ReadModel Apply(FuturesContractV3ReadModel value) => value with
    {
        ReviewState = ReviewState, MappingVersion = MappingVersion, EvidenceId = EvidenceId,
        EffectiveFromUtc = EffectiveFromUtc, EffectiveUntilUtc = EffectiveUntilUtc, ExchangeTimeZoneId = ExchangeTimeZoneId,
        CalendarVersion = CalendarVersion, LastTradingUtc = LastTradingUtc, SettlementStyle = SettlementStyle
    };
    public FuturesOptionContractReadModel Apply(FuturesOptionContractReadModel value) => value with
    {
        ReviewState = ReviewState, MappingVersion = MappingVersion, EvidenceId = EvidenceId,
        EffectiveFromUtc = EffectiveFromUtc, EffectiveUntilUtc = EffectiveUntilUtc, ExchangeTimeZoneId = ExchangeTimeZoneId,
        CalendarVersion = CalendarVersion, LastTradingUtc = LastTradingUtc, SettlementStyle = SettlementStyle,
        ExerciseStyle = ExerciseStyle, PremiumStyle = PremiumStyle, PremiumTickRule = PremiumTickRule,
        TickRuleVersion = TickRuleVersion, DayCount = DayCount, ExerciseCutoffUtc = ExerciseCutoffUtc,
        ExerciseResultContractId = string.IsNullOrWhiteSpace(ExerciseResultContractId) ? null : ExerciseResultContractId
    };
    public static ReferenceConventionEditor From(FuturesContractV3ReadModel value) => new()
    {
        ReviewState = value.ReviewState, MappingVersion = value.MappingVersion ?? "", EvidenceId = value.EvidenceId ?? "",
        EffectiveFromUtc = value.EffectiveFromUtc, EffectiveUntilUtc = value.EffectiveUntilUtc,
        ExchangeTimeZoneId = value.ExchangeTimeZoneId ?? "", CalendarVersion = value.CalendarVersion ?? "",
        LastTradingUtc = value.LastTradingUtc, SettlementStyle = value.SettlementStyle
    };
    public static ReferenceConventionEditor From(FuturesOptionContractReadModel value) => new()
    {
        ReviewState = value.ReviewState, MappingVersion = value.MappingVersion ?? "", EvidenceId = value.EvidenceId ?? "",
        EffectiveFromUtc = value.EffectiveFromUtc, EffectiveUntilUtc = value.EffectiveUntilUtc,
        ExchangeTimeZoneId = value.ExchangeTimeZoneId ?? "", CalendarVersion = value.CalendarVersion ?? "",
        LastTradingUtc = value.LastTradingUtc, SettlementStyle = value.SettlementStyle,
        UnderlyingContractId = value.UnderlyingContractId ?? "", ExerciseStyle = value.ExerciseStyle,
        PremiumStyle = value.PremiumStyle, PremiumTickRule = value.PremiumTickRule,
        TickRuleVersion = value.TickRuleVersion ?? "", DayCount = value.DayCount,
        ExerciseCutoffUtc = value.ExerciseCutoffUtc, ExerciseResultContractId = value.ExerciseResultContractId ?? ""
    };
}
