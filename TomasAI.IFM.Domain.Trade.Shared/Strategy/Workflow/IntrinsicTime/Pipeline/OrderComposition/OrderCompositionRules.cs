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
[MessagePackObject]
public sealed record CompositionParameters
{
    [JsonRequired, Key(0)] public int LoadingMilliseconds { get; init; }
    [JsonRequired, Key(1)] public int ExecutionMilliseconds { get; init; }
    [JsonRequired, Key(2)] public int CandidateLifetimeMilliseconds { get; init; }
    [JsonRequired, Key(3)] public int MaximumQuoteAgeMilliseconds { get; init; }
    [JsonRequired, Key(4)] public int MaximumQuoteSkewMilliseconds { get; init; }
    [JsonRequired, Key(5)] public decimal MinimumDisplayedSize { get; init; }
    [JsonRequired, Key(6)] public decimal ParticipationFraction { get; init; }
    [JsonRequired, Key(7)] public decimal MaximumUnderlyingSpreadTicks { get; init; }
    [JsonRequired, Key(8)] public decimal MaximumLegSpreadTicks { get; init; }
    [JsonRequired, Key(9)] public decimal MaximumComboSpreadTicks { get; init; }
    [JsonRequired, Key(10)] public decimal TargetDaysToExpiry { get; init; }
    [JsonRequired, Key(11)] public decimal MinimumDaysToExpiry { get; init; }
    [JsonRequired, Key(12)] public decimal MaximumDaysToExpiry { get; init; }
    [JsonRequired, Key(13)] public decimal TargetLegDelta { get; init; }
    [JsonRequired, Key(14)] public decimal LegDeltaTolerance { get; init; }
    [JsonRequired, Key(15)] public decimal TargetPutDelta { get; init; }
    [JsonRequired, Key(16)] public decimal TargetCallDelta { get; init; }
    [JsonRequired, Key(17)] public decimal TargetNetDelta { get; init; }
    [JsonRequired, Key(18)] public decimal BalanceTolerance { get; init; }
    [JsonRequired, Key(19)] public decimal MinimumCreditToWidth { get; init; }
    [JsonRequired, Key(20)] public decimal MaximumDebitToWidth { get; init; }
    [JsonRequired, Key(21)] public decimal MinimumCreditTicks { get; init; }
    [JsonRequired, Key(22)] public decimal MinimumRewardToRisk { get; init; }
    [JsonRequired, Key(23)] public decimal MidpointToNaturalFraction { get; init; }
    [JsonRequired, Key(24)] public int MaximumAdverseMoveTicks { get; init; }
    [JsonRequired, Key(25)] public decimal FeePerContract { get; init; }
    [JsonRequired, Key(26)] public decimal SlippageTicksPerLeg { get; init; }
    [JsonRequired, Key(27)] public decimal FuturesPlannedDistance { get; init; }
    [JsonRequired, Key(28)] public decimal FuturesStressDistance { get; init; }
    [JsonRequired, Key(29)] public decimal FuturesRollHours { get; init; }
}

[MessagePackObject]
public sealed record CompositionParameterBound
{
    [JsonRequired, Key(0)] public CompositionParameter Parameter { get; init; }
    [JsonRequired, Key(1)] public decimal Minimum { get; init; }
    [JsonRequired, Key(2)] public decimal Maximum { get; init; }
    [JsonRequired, Key(3)] public decimal Grid { get; init; }
}

public enum CompositionParameter { LoadingMilliseconds, ExecutionMilliseconds, CandidateLifetimeMilliseconds, MaximumQuoteAgeMilliseconds, MaximumQuoteSkewMilliseconds, MinimumDisplayedSize, ParticipationFraction, MaximumUnderlyingSpreadTicks, MaximumLegSpreadTicks, MaximumComboSpreadTicks, TargetDaysToExpiry, MinimumDaysToExpiry, MaximumDaysToExpiry, TargetLegDelta, LegDeltaTolerance, TargetPutDelta, TargetCallDelta, TargetNetDelta, BalanceTolerance, MinimumCreditToWidth, MaximumDebitToWidth, MinimumCreditTicks, MinimumRewardToRisk, MidpointToNaturalFraction, MaximumAdverseMoveTicks, FeePerContract, SlippageTicksPerLeg, FuturesPlannedDistance, FuturesStressDistance, FuturesRollHours }
public enum CompositionOperation { Undefined=0, Set=1, Add=2, Subtract=3, Multiply=4, Minimum=5, Maximum=6 }
public enum CompositionComparison { Undefined=0, Equal=1, Less=2, LessOrEqual=3, Greater=4, GreaterOrEqual=5, In=6, All=7, Any=8 }
public enum CompositionFeature { Undefined=0, RegimeConfidence=1, SelectionConfidence=2, ForwardPrice=3, ImpliedVolatility=4 }
[MessagePackObject]
public sealed record CompositionPredicate
{
    [JsonRequired, Key(0)] public CompositionComparison Comparison { get; init; }
    [JsonRequired, Key(1)] public CompositionFeature Feature { get; init; }
    [JsonRequired, Key(2)] public ImmutableArray<decimal> Values { get; init; } = [];
    [JsonRequired, Key(3)] public ImmutableArray<CompositionPredicate> Children { get; init; } = [];
    [JsonRequired, Key(4)] public bool Required { get; init; }
}

[MessagePackObject]
public sealed record CompositionAdjustment
{
    [JsonRequired, Key(0)] public string Code { get; init; } = "";
    [JsonRequired, Key(1)] public int Priority { get; init; }
    [JsonRequired, Key(2)] public CompositionPredicate Predicate { get; init; } = new();
    [JsonRequired, Key(3)] public CompositionParameter Parameter { get; init; }
    [JsonRequired, Key(4)] public CompositionOperation Operation { get; init; }
    [JsonRequired, Key(5)] public decimal Operand { get; init; }
}

[MessagePackObject]
public sealed record CompositionRuleEvidence
{
    [Key(0)] public string Code { get; init; } = "";
    [Key(1)] public string Status { get; init; } = "";
    [Key(2)] public CompositionParameter Parameter { get; init; }
    [Key(3)] public decimal Before { get; init; }
    [Key(4)] public decimal Unclamped { get; init; }
    [Key(5)] public decimal After { get; init; }
}

[MessagePackObject]
public sealed record CompositionResolvedParameters
{
    [Key(0)] public CompositionParameters Values { get; init; } = new();
    [Key(1)] public ImmutableArray<CompositionRuleEvidence> Evidence { get; init; } = [];
    [Key(2)] public string Hash { get; init; } = "";
}

[MessagePackObject]
public sealed record CompositionVariantRules
{
    [JsonRequired, Key(0)] public CatalogKey VariantKey { get; init; } = default!;
    [JsonRequired, Key(1)] public CatalogKey StructureKey { get; init; } = default!;
    [JsonRequired, Key(2)] public CompositionParameters BaseParameters { get; init; } = new();
    [JsonRequired, Key(3)] public ImmutableArray<CompositionParameterBound> HardBounds { get; init; } = [];
    [JsonRequired, Key(4)] public ImmutableArray<CompositionAdjustment> AdjustmentRules { get; init; } = [];
    [JsonRequired, Key(5)] public ImmutableArray<decimal> AllowedWidths { get; init; } = [];
    [JsonRequired, Key(6)] public bool RequireSymmetricWings { get; init; }
    [JsonRequired, Key(7)] public string DeltaUnits { get; init; } = "";
    [JsonRequired, Key(8)] public string RankingVersion { get; init; } = "";
}

[MessagePackObject]
public sealed record OrderCompositionRules
{
    [JsonRequired, Key(0)] public short SchemaVersion { get; init; }
    [JsonRequired, Key(1)] public string AlgorithmVersion { get; init; } = "";
    [JsonRequired, Key(2)] public string PricerVersion { get; init; } = "";
    [JsonRequired, Key(3)] public TimeFrameType SupportedHorizon { get; init; }
    [JsonRequired, Key(4)] public string InstrumentRoot { get; init; } = "";
    [JsonRequired, Key(5)] public string Currency { get; init; } = "";
    [JsonRequired, Key(6)] public ImmutableArray<CompositionVariantRules> VariantRules { get; init; } = [];
}

[MessagePackObject]
public sealed record CompositionBinding
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public SelectionCandidateIntent Selected { get; init; } = new();
    [Key(2)] public SelectionCatalogDefinitionSnapshot RulesDefinition { get; init; } = new();
    [Key(3)] public SelectionCatalogDefinitionSnapshot RulesSchema { get; init; } = new();
    [Key(4)] public OrderCompositionRules Rules { get; init; } = new();
    [Key(5)] public string BuilderCode { get; init; } = "";
    [Key(6)] public int BuilderVersion { get; init; }
    [Key(7)] public DateTime FrozenAtUtc { get; init; }
    [Key(8)] public DateTime ValidUntilUtc { get; init; }
    [Key(9)] public string BindingSha256 { get; init; } = "";
}

/// <summary>Strict rules reader and semantic hash. Existing selection/Portfolio hash formats remain unchanged.</summary>
public static class CompositionHash
{
    public static string Compute<T>(T value) => CompositionSemanticHash.Compute(value);
    public static string Candidate(CompositionCandidate value) => Compute(value with { CandidateHash="" });
    public static string Binding(CompositionBinding value) => Compute(value with { BindingSha256="" });
}
public sealed class CompositionException(string code) : ArgumentException(code)
{
    public string ReasonCode { get; } = code;
}
public static class CompositionRulesContract
{
    public const string AlgorithmVersion="OrderComposer/v1";
    public const string Role="OrderCompositionRules";
    public const string RankingVersion="Lexicographic/v1";
    static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static OrderCompositionRules Read(string json)
    {
        Configuration.TradeSelection.TradeSelectionPolicy.CheckJson(json);
        var rules=JsonSerializer.Deserialize<OrderCompositionRules>(json,Options) ?? throw new CompositionException("OC.CONFIG.MISSING");
        Validate(rules);return rules;
    }
    public static string Serialize(OrderCompositionRules rules){Validate(rules);return JsonSerializer.Serialize(rules,Options);}
    public static void Require(bool condition,string reason){if(!condition)throw new CompositionException(reason);}
    public static void Validate(OrderCompositionRules r)
    {
        Require(r.SchemaVersion==1 && r.AlgorithmVersion==AlgorithmVersion && !string.IsNullOrWhiteSpace(r.PricerVersion)
            && r.SupportedHorizon is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly
            && r.InstrumentRoot=="ES" && r.Currency=="USD" && !r.VariantRules.IsDefaultOrEmpty && r.VariantRules.Length<=12,"OC.CONFIG.RULE_INVALID");
        Require(r.VariantRules.Select(x=>x.VariantKey).Distinct().Count()==r.VariantRules.Length,"OC.CONFIG.AMBIGUOUS");
        foreach(var v in r.VariantRules)
        {
            Require(v.VariantKey is {Kind:StrategyCatalogKind.Variant,Version:>0} && v.StructureKey is {Kind:StrategyCatalogKind.Structure,Version:>0}
                && v.DeltaUnits=="UnderlyingEquivalent" && v.RankingVersion==RankingVersion && !v.HardBounds.IsDefault
                && !v.AdjustmentRules.IsDefault && !v.AllowedWidths.IsDefault && v.AdjustmentRules.Length<=64
                && v.HardBounds.Length<=64 && v.AllowedWidths.Length<=64 && v.AllowedWidths.All(x=>x>0)
                && v.AllowedWidths.Distinct().Count()==v.AllowedWidths.Length,"OC.CONFIG.RULE_INVALID");
            Validate(v.BaseParameters);
            Require(v.HardBounds.Select(x=>x.Parameter).Distinct().Count()==v.HardBounds.Length
                && v.HardBounds.All(x=>Enum.IsDefined(x.Parameter) && x.Minimum<=x.Maximum && x.Grid>0)
                && v.AdjustmentRules.Select(x=>x.Code).Distinct(StringComparer.Ordinal).Count()==v.AdjustmentRules.Length,"OC.CONFIG.RULE_INVALID");
            foreach(var a in v.AdjustmentRules)
            {
                Require(!string.IsNullOrWhiteSpace(a.Code) && a.Code.Length<=64 && Enum.IsDefined(a.Parameter)
                    && a.Parameter is not (CompositionParameter.LoadingMilliseconds or CompositionParameter.ExecutionMilliseconds)
                    && a.Operation!=CompositionOperation.Undefined && Enum.IsDefined(a.Operation)
                    && v.HardBounds.Any(x=>x.Parameter==a.Parameter),"OC.CONFIG.RULE_INVALID");
                int leaves=0;ValidatePredicate(a.Predicate,1,ref leaves);
            }
        }
    }
    static void ValidatePredicate(CompositionPredicate p,int depth,ref int leaves)
    {
        Require(p is not null && depth<=8 && !p.Children.IsDefault && !p.Values.IsDefault && Enum.IsDefined(p.Comparison)
            && p.Comparison!=CompositionComparison.Undefined,"OC.CONFIG.RULE_INVALID");
        if(p.Comparison is CompositionComparison.All or CompositionComparison.Any)
        {Require(p.Children.Length is >0 and <=64 && p.Values.IsEmpty,"OC.CONFIG.RULE_INVALID");foreach(var c in p.Children)ValidatePredicate(c,depth+1,ref leaves);}
        else
        {Require(++leaves<=64 && p.Children.IsEmpty && p.Feature!=CompositionFeature.Undefined && Enum.IsDefined(p.Feature)
            && p.Values.Length is >0 and <=64 && (p.Comparison==CompositionComparison.In || p.Values.Length==1),"OC.CONFIG.RULE_INVALID");}
    }
    public static void Validate(CompositionParameters p)
    {
        Require(p.LoadingMilliseconds is >=1 and <=30000 && p.ExecutionMilliseconds is >=1 and <=30000
            && p.CandidateLifetimeMilliseconds is >=1 and <=30000 && p.MaximumQuoteAgeMilliseconds is >=1 and <=5000
            && p.MaximumQuoteSkewMilliseconds is >=0 and <=2000 && p.MinimumDisplayedSize>=1
            && p.ParticipationFraction is >0 and <=1 && p.MaximumUnderlyingSpreadTicks>0 && p.MaximumLegSpreadTicks>0
            && p.MaximumComboSpreadTicks>0 && p.MinimumDaysToExpiry>=0 && p.MaximumDaysToExpiry>=p.MinimumDaysToExpiry
            && p.TargetDaysToExpiry>=p.MinimumDaysToExpiry && p.TargetDaysToExpiry<=p.MaximumDaysToExpiry
            && p.TargetLegDelta is >0 and <=1 && p.LegDeltaTolerance is >=0 and <=1 && p.TargetPutDelta is >0 and <=1
            && p.TargetCallDelta is >0 and <=1 && p.TargetNetDelta is >=-1 and <=1 && p.BalanceTolerance is >=0 and <=1
            && p.MinimumCreditToWidth is >=0 and <1 && p.MaximumDebitToWidth is >0 and <1 && p.MinimumCreditTicks>=1
            && p.MinimumRewardToRisk>=0 && p.MidpointToNaturalFraction is >=0 and <=1 && p.MaximumAdverseMoveTicks>=0
            && p.FeePerContract>=0 && p.SlippageTicksPerLeg>=0 && p.FuturesPlannedDistance>0
            && p.FuturesStressDistance>=p.FuturesPlannedDistance && p.FuturesRollHours>=0,"OC.CONFIG.RULE_INVALID");
    }
}
