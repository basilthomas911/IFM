using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
/// <summary>Version-one construction constraints read from the exact OrderComposition policy. No quotes or sizing.</summary>
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
    static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new TradeSelectionPolicy.CanonicalDecimalConverter()}};
    public static SelectionConstructionPolicy Read(string json)
    {
        TradeSelectionPolicy.CheckJson(json);
        var p=JsonSerializer.Deserialize<SelectionConstructionPolicy>(json,Options)??throw new ArgumentException("Missing construction policy.");p.Validate();return p;
    }
    public string Serialize(){Validate();return JsonSerializer.Serialize(this,Options);}
    public string Hash()=>TradeSelectionPolicy.HashJson(Serialize());
    public void Validate()=>TradeSelectionContracts.Require(SchemaVersion==1 && ParameterSetId!=Guid.Empty && Version>0 && MaximumLegs is >=1 and <=4
        && MinimumDaysToExpiry>=1 && MaximumDaysToExpiry>=MinimumDaysToExpiry && MaximumDaysToExpiry<=730
        && MinimumWingWidth>=0 && MaximumWingWidth>=MinimumWingWidth && MaximumWingWidth<=10000 && DeltaUnits=="UnderlyingEquivalent" && MaximumDeltaTolerance is >=0 and <=1,
        "TS.CONFIG.COMPOSITION_SCHEMA","Invalid or unsupported construction constraints.");
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
