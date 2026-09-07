using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;

/// <summary>Canonical complete policy payloads and fail-closed validation for catalog publication and execution.</summary>
public static class TradeSelectionPolicy
{
    static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow, MaxDepth=32,
        Converters={new CanonicalDecimalConverter()}
    };
    public static bool IsHorizon(TimeFrameType value) => value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly;
    public static string Serialize(TradeSelectionParameterSet value) { Validate(value); return JsonSerializer.Serialize(value,Options); }
    public static string Hash(TradeSelectionParameterSet value) => HashJson(Serialize(value));
    public static string HashJson(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static TradeSelectionParameterSet Read(string json)
    {
        CheckJson(json);
        var value=JsonSerializer.Deserialize<TradeSelectionParameterSet>(json,Options) ?? throw Invalid("Null policy.");
        Validate(value); return value;
    }
    public static TradeSelectionSpecializedRules ReadSpecialized(string json,TradeSelectionParameterSet common)
    {
        CheckJson(json);
        var value=JsonSerializer.Deserialize<TradeSelectionSpecializedRules>(json,Options) ?? throw Invalid("Null specialized rules.");
        Require(value.SchemaVersion==1,"Unknown specialized schema.");
        ValidateRules(value.Rules);
        foreach(var r in value.Rules)
        {
            Subset(r.AllowedRegimeDirections,common.AllowedRegimeDirections);
            Subset(r.AllowedTrendPhases,common.AllowedTrendPhases);
            Subset(r.AllowedTrendStrengths,common.AllowedTrendStrengths);
            Subset(r.AllowedStructureClassifications,common.AllowedStructureClassifications);
            Subset(r.AllowedAssessmentConditions,common.AllowedAssessmentConditions);
            Subset(r.AllowedVolatilityBehavior,common.AllowedVolatilityBehavior);
        }
        return value;
    }
    public static void Validate(TradeSelectionParameterSet p)
    {
        ArgumentNullException.ThrowIfNull(p);
        Require(p.SchemaVersion==1 && p.ParameterSetId!=Guid.Empty && p.Version>0,"Invalid policy identity/schema.");
        Require(p.InstrumentRoot=="ES" && IsHorizon(p.TargetHorizon) && !string.IsNullOrWhiteSpace(p.ProfileCode) && p.ProfileCode.Length<=100,"Invalid root/horizon/profile.");
        Require(p.MinimumRegimeConfidence is >=0 and <=1 && p.MinimumAssessmentConfidence is >=0 and <=1,"Confidence must be in [0,1].");
        Require(p.UnknownEvidencePolicy==UnknownEvidencePolicy.NoTrade,"Unsupported Unknown policy.");
        Require(p.RankingPolicyVersion=="ts-rank-v1" && p.ReasonCodeCatalogVersion=="ts-reasons-v1" && p.SummaryTemplateVersion=="ts-summary-v1" && p.DirectionMappingVersion=="ts-direction-v1","Unsupported semantic version.");
        Require(p.MaximumAssignments is >=1 and <=16 && p.MaximumCandidates is >=1 and <=64 && p.MaximumCatalogDefinitions is >=1 and <=256,"Invalid cardinality limits.");
        Require(p.MaximumBindingPayloadBytes is >=65536 and <=262144 && p.MaximumResultPayloadBytes is >=65536 and <=524288,"Invalid payload limits.");
        Require(p.MaximumExecutionMilliseconds is >=1 and <=60000 && p.ResultLifetimeSeconds is >=1 and <=300 && p.FutureClockSkewSeconds is >=0 and <=60,"Invalid time limits.");
        Set(p.AllowedRegimeDirections); Set(p.AllowedTrendPhases); Set(p.AllowedTrendStrengths); Set(p.AllowedRegimeQualities);
        Set(p.AllowedRegimeVolatilityLevels); Set(p.AllowedRegimeVolatilityChanges); Set(p.AllowedStructureClassifications); Set(p.RejectedInheritedRestrictions);
        Set(p.AllowedAssessmentConditions); Set(p.AllowedLiquidity); Set(p.AllowedSessions); Set(p.AllowedEventRisk); Set(p.AllowedStress);
        Set(p.AllowedVolatilityBehavior); Set(p.AllowedTriggerAlignment); Set(p.AllowedAssessmentDataQuality);
        Require(p.RejectedInheritedRestrictions.Contains(RegimeRestriction.NoNewTrade),"NoNewTrade must be rejected.");
        ValidateRules(p.VariantRules);
    }
    public static string Signature(SelectionVariantRule r) => $"{r.BuilderCapabilityCode}:{r.BuilderCapabilityVersion}:{r.Side}:{r.Bias}:{r.PremiumMode}";
    public static void ValidateRules(SelectionVariantRule[] rules)
    {
        Require(rules.Length==12 && rules.All(x=>x is not null) && rules.Select(Signature).Distinct(StringComparer.Ordinal).Count()==12,"Exactly twelve unique variant signatures are required.");
        foreach(var r in rules)
        {
            Require(r.BuilderCapabilityVersion==1 && r.Preference is >=0 and <=100000,"Invalid variant version/preference.");
            var valid=(r.BuilderCapabilityCode,r.Side,r.Bias,r.PremiumMode) switch
            {
                ("Future","Long","Bullish","None") or ("Future","Short","Bearish","None") => true,
                ("CallVertical","Long","Bullish","Debit") or ("CallVertical","Short","Bearish","Credit") => true,
                ("PutVertical","Long","Bearish","Debit") or ("PutVertical","Short","Bullish","Credit") => true,
                ("IronCondor","Short","Balanced" or "Bullish" or "Bearish","Credit") or ("IronCondor","Long","Balanced" or "Bullish" or "Bearish","Debit") => true,
                _ => false
            };
            Require(valid,"Unsupported variant signature.");
            Set(r.AllowedRegimeDirections); Set(r.AllowedTrendPhases); Set(r.AllowedTrendStrengths); Set(r.AllowedStructureClassifications); Set(r.AllowedAssessmentConditions); Set(r.AllowedVolatilityBehavior);
            var direction=r.Bias=="Balanced"?RegimeDirection.Neutral:r.Bias=="Bullish"?RegimeDirection.Up:RegimeDirection.Down;
            Require(r.AllowedRegimeDirections.All(x=>x==direction),"Variant direction cannot invert its declared bias.");
        }
    }
    public static void CheckJson(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && Encoding.UTF8.GetByteCount(json)<=524288,"Invalid JSON size.");
        using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=32});
        CheckElement(document.RootElement);
    }
    static void CheckElement(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object)
        {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var prop in element.EnumerateObject()) { Require(keys.Add(prop.Name),"Duplicate JSON property."); CheckElement(prop.Value); }
        }
        else if(element.ValueKind==JsonValueKind.Array) foreach(var child in element.EnumerateArray()) CheckElement(child);
    }
    static void Set<T>(T[] values) where T:struct,Enum
    {
        Require(values.Length is >0 and <=64 && values.Distinct().Count()==values.Length && values.All(x=>Enum.IsDefined(x) && x.ToString() is not ("Unknown" or "Undefined")),"Invalid observation set.");
    }
    static void Subset<T>(T[] values,T[] allowed) => Require(values.All(x=>allowed.Contains(x)),"Specialized rules cannot broaden common constraints.");
    static ArgumentException Invalid(string message) => new("TS.CONFIG.INVALID: "+message);
    static void Require(bool condition,string message) { if(!condition) throw Invalid(message); }
    internal sealed class CanonicalDecimalConverter:JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer,decimal value,JsonSerializerOptions options)=>writer.WriteRawValue(value.ToString("G29",CultureInfo.InvariantCulture));
    }
}
