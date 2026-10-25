using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
public static class CompositionRulesContract
{
    public const string AlgorithmVersion = "OrderComposer/v1";
    public const string Role = "OrderCompositionRules";
    public const string RankingVersion = "Lexicographic/v1";
    static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static OrderCompositionRules Read(string json)
    {
        Configuration.TradeSelection.TradeSelectionPolicy.CheckJson(json);
        var rules = JsonSerializer.Deserialize<OrderCompositionRules>(json, Options) ?? throw new CompositionException("OC.CONFIG.MISSING");
        Validate(rules); return rules;
    }
    public static string Serialize(OrderCompositionRules rules) { Validate(rules); return JsonSerializer.Serialize(rules, Options); }
    public static void Require(bool condition, string reason) { if (!condition) throw new CompositionException(reason); }
    public static void Validate(OrderCompositionRules r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Require(r.SchemaVersion == 1 && string.Equals(r.AlgorithmVersion,AlgorithmVersion,StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(r.PricerVersion)
            && r.SupportedHorizon is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly
            && string.Equals(r.InstrumentRoot,"ES",StringComparison.Ordinal) && string.Equals(r.Currency,"USD",StringComparison.Ordinal) && !r.VariantRules.IsDefaultOrEmpty && r.VariantRules.Length <= 12, "OC.CONFIG.RULE_INVALID");
        Require(r.VariantRules.Select(x => x.VariantKey).Distinct().Count() == r.VariantRules.Length, "OC.CONFIG.AMBIGUOUS");
        foreach (var v in r.VariantRules)
        {
            Require(v.VariantKey is { Kind: StrategyCatalogKind.Variant, Version: > 0 } && v.StructureKey is { Kind: StrategyCatalogKind.Structure, Version: > 0 }
                && string.Equals(v.DeltaUnits,"UnderlyingEquivalent",StringComparison.Ordinal) && string.Equals(v.RankingVersion,RankingVersion,StringComparison.Ordinal) && !v.HardBounds.IsDefault
                && !v.AdjustmentRules.IsDefault && !v.AllowedWidths.IsDefault && v.AdjustmentRules.Length <= 64
                && v.HardBounds.Length <= 64 && v.AllowedWidths.Length <= 64 && v.AllowedWidths.All(x => x > 0)
                && v.AllowedWidths.Distinct().Count() == v.AllowedWidths.Length, "OC.CONFIG.RULE_INVALID");
            Validate(v.BaseParameters);
            Require(v.HardBounds.Select(x => x.Parameter).Distinct().Count() == v.HardBounds.Length
                && v.HardBounds.All(x => Enum.IsDefined(x.Parameter) && x.Minimum <= x.Maximum && x.Grid > 0)
                && v.AdjustmentRules.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == v.AdjustmentRules.Length, "OC.CONFIG.RULE_INVALID");
            foreach (var a in v.AdjustmentRules)
            {
                Require(!string.IsNullOrWhiteSpace(a.Code) && a.Code.Length <= 64 && Enum.IsDefined(a.Parameter)
                    && a.Parameter is not (CompositionParameter.LoadingMilliseconds or CompositionParameter.ExecutionMilliseconds)
                    && a.Operation != CompositionOperation.Undefined && Enum.IsDefined(a.Operation)
                    && v.HardBounds.Any(x => x.Parameter == a.Parameter), "OC.CONFIG.RULE_INVALID");
                int leaves = 0; ValidatePredicate(a.Predicate, 1, ref leaves);
            }
        }
    }
    public static void Validate(CompositionParameters p)
    {
        ArgumentNullException.ThrowIfNull(p);
        Require(p.LoadingMilliseconds is >= 1 and <= 30000 && p.ExecutionMilliseconds is >= 1 and <= 30000
            && p.CandidateLifetimeMilliseconds is >= 1 and <= 30000 && p.MaximumQuoteAgeMilliseconds is >= 1 and <= 5000
            && p.MaximumQuoteSkewMilliseconds is >= 0 and <= 2000 && p.MinimumDisplayedSize >= 1
            && p.ParticipationFraction is > 0 and <= 1 && p.MaximumUnderlyingSpreadTicks > 0 && p.MaximumLegSpreadTicks > 0
            && p.MaximumComboSpreadTicks > 0 && p.MinimumDaysToExpiry >= 0 && p.MaximumDaysToExpiry >= p.MinimumDaysToExpiry
            && p.TargetDaysToExpiry >= p.MinimumDaysToExpiry && p.TargetDaysToExpiry <= p.MaximumDaysToExpiry
            && p.TargetLegDelta is > 0 and <= 1 && p.LegDeltaTolerance is >= 0 and <= 1 && p.TargetPutDelta is > 0 and <= 1
            && p.TargetCallDelta is > 0 and <= 1 && p.TargetNetDelta is >= -1 and <= 1 && p.BalanceTolerance is >= 0 and <= 1
            && p.MinimumCreditToWidth is >= 0 and < 1 && p.MaximumDebitToWidth is > 0 and < 1 && p.MinimumCreditTicks >= 1
            && p.MinimumRewardToRisk >= 0 && p.MidpointToNaturalFraction is >= 0 and <= 1 && p.MaximumAdverseMoveTicks >= 0
            && p.FeePerContract >= 0 && p.SlippageTicksPerLeg >= 0 && p.FuturesPlannedDistance > 0
            && p.FuturesStressDistance >= p.FuturesPlannedDistance && p.FuturesRollHours >= 0, "OC.CONFIG.RULE_INVALID");
    }
    static void ValidatePredicate(CompositionPredicate p, int depth, ref int leaves)
    {
        Require(p is not null && depth <= 8 && !p.Children.IsDefault && !p.Values.IsDefault && Enum.IsDefined(p.Comparison)
            && p.Comparison != CompositionComparison.Undefined, "OC.CONFIG.RULE_INVALID");
        if (p.Comparison is CompositionComparison.All or CompositionComparison.Any)
        { Require(p.Children.Length is > 0 and <= 64 && p.Values.IsEmpty, "OC.CONFIG.RULE_INVALID"); foreach (var c in p.Children) ValidatePredicate(c, depth + 1, ref leaves); }
        else
        {
            Require(++leaves <= 64 && p.Children.IsEmpty && p.Feature != CompositionFeature.Undefined && Enum.IsDefined(p.Feature)
            && p.Values.Length is > 0 and <= 64 && (p.Comparison == CompositionComparison.In || p.Values.Length == 1), "OC.CONFIG.RULE_INVALID");
        }
    }
}
