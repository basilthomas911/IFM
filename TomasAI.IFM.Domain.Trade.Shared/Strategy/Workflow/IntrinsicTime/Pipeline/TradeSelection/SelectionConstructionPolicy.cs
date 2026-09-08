using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
/// <summary>Construction constraints from the exact policy; version two additionally pins a reviewed bounded market universe.</summary>
public sealed record SelectionConstructionPolicy
{
    [JsonRequired] public short SchemaVersion {get;init;}
    [JsonRequired] public Guid ParameterSetId {get;init;}
    [JsonRequired] public int Version {get;init;}
    [JsonRequired] public int MaximumLegs {get;init;}
    [JsonRequired] public int MinimumDaysToExpiry {get;init;}
    [JsonRequired] public int MaximumDaysToExpiry {get;init;}
    [JsonRequired] public decimal MinimumWingWidth {get;init;}
    [JsonRequired] public decimal MaximumWingWidth {get;init;}
    [JsonRequired] public string DeltaUnits {get;init;}=string.Empty;
    [JsonRequired] public decimal MaximumDeltaTolerance {get;init;}
    [JsonPropertyName("marketData"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? MarketData {get;init;}
    static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new TradeSelectionPolicy.CanonicalDecimalConverter()}};
    public static SelectionConstructionPolicy Read(string json)
    {
        TradeSelectionPolicy.CheckJson(json);
        var p=JsonSerializer.Deserialize<SelectionConstructionPolicy>(json,Options)??throw new ArgumentException("Missing construction policy.");p.Validate();return p;
    }
    public string Serialize(){Validate();return JsonSerializer.Serialize(this,Options);}
    public string Hash()=>TradeSelectionPolicy.HashJson(Serialize());
    public void Validate()=>TradeSelectionContracts.Require((SchemaVersion==1 && MarketData is null
            || SchemaVersion==2 && ValidMarketData())
        && ParameterSetId!=Guid.Empty && Version>0 && MaximumLegs is >=1 and <=4
        && MinimumDaysToExpiry>=1 && MaximumDaysToExpiry>=MinimumDaysToExpiry && MaximumDaysToExpiry<=730
        && MinimumWingWidth>=0 && MaximumWingWidth>=MinimumWingWidth && MaximumWingWidth<=10000 && DeltaUnits=="UnderlyingEquivalent" && MaximumDeltaTolerance is >=0 and <=1,
        "TS.CONFIG.COMPOSITION_SCHEMA","Invalid or unsupported construction constraints.");

    bool ValidMarketData()
    {
        if (MarketData is not { ValueKind: JsonValueKind.Object } data) return false;
        string[] required = ["SchemaVersion", "Dataset", "Root", "ValueDate", "IncludeOptions", "ScopeComplete", "MaturityDate", "Options", "Futures"];
        string[] optional = ["Calendar", "Publication", "Conversion"];
        if (required.Any(name => !data.TryGetProperty(name, out _))
            || data.EnumerateObject().Any(p => !required.Contains(p.Name, StringComparer.Ordinal) && !optional.Contains(p.Name, StringComparer.Ordinal))) return false;
        if (data.GetProperty("SchemaVersion").ValueKind != JsonValueKind.Number || !data.GetProperty("SchemaVersion").TryGetInt32(out var schema) || schema != 1
            || data.GetProperty("Dataset").ValueKind != JsonValueKind.String || data.GetProperty("Dataset").GetString() != "GLBX.MDP3"
            || data.GetProperty("Root").ValueKind != JsonValueKind.String || data.GetProperty("Root").GetString() != "ES"
            || data.GetProperty("ScopeComplete").ValueKind != JsonValueKind.True
            || data.GetProperty("IncludeOptions").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || data.GetProperty("ValueDate").ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(data.GetProperty("ValueDate").GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)
            || data.GetProperty("MaturityDate").ValueKind != JsonValueKind.String) return false;
        var options = data.GetProperty("Options"); var futures = data.GetProperty("Futures");
        if (options.ValueKind != JsonValueKind.Array || futures.ValueKind != JsonValueKind.Array
            || options.GetArrayLength() > 512 || futures.GetArrayLength() > 16) return false;
        if (!data.GetProperty("IncludeOptions").GetBoolean()) return options.GetArrayLength() == 0 && futures.GetArrayLength() > 0;
        return futures.GetArrayLength() == 0
            && DateOnly.TryParseExact(data.GetProperty("MaturityDate").GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)
            && optional.All(name => data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object);
    }
    public void ValidateCandidate(SelectionCatalogDefinitionSnapshot structure,SelectionCatalogDefinitionSnapshot variant)
    {
        TradeSelectionContracts.Require(structure.Legs.Length<=MaximumLegs,"TS.CONFIG.COMPOSITION_SCHEMA","Composition policy cannot construct this leg count.");
        if(structure.Legs.Any(x=>x.InstrumentClass=="FuturesOption"))
        {
            var settings=variant.SettingsJson;
            using var doc=JsonDocument.Parse(settings);var root=doc.RootElement;
            TradeSelectionContracts.Require(MinimumWingWidth>0 && root.GetProperty("MinimumWingWidth").GetDecimal()>=MinimumWingWidth && root.GetProperty("MaximumWingWidth").GetDecimal()<=MaximumWingWidth
                && root.GetProperty("BalanceTolerance").GetDecimal()<=MaximumDeltaTolerance,"TS.CONFIG.COMPOSITION_SCHEMA","Variant and composition constraints do not agree.");
        }
    }
}
