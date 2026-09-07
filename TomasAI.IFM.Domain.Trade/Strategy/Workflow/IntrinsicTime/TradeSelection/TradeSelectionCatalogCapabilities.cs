using System.Text.Json;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
/// <summary>Only implemented selector capabilities. Builder and risk capabilities must be supplied by their owners.</summary>
public static class TradeSelectionCatalogCapabilities
{
    public static IStrategyCatalogCapabilityValidator[] Create()=>[new SelectorValidator(new("evaluator","RegimeAligned",1)),new SelectorValidator(new("data","AcceptedMarketAssessment",1)),new SelectorValidator(new("validator","StructureVariant",1)),new SelectorValidator(new("validator","TradeSelectionVariants",1))];
    sealed class SelectorValidator(CatalogCapability capability):IStrategyCatalogCapabilityValidator
    {
        public CatalogCapability Capability=>capability;
        public void Validate(StrategyCatalogDefinition owner,IReadOnlyDictionary<CatalogKey,StoredStrategyCatalogDefinition> dependencies)
        {
            if(capability.Code=="TradeSelectionVariants")
            {
                Require(owner.Key.Kind is StrategyCatalogKind.ParameterSchema or StrategyCatalogKind.ParameterSet,"TS.CONFIG.INVALID","Specialized selector capability requires ParameterSchema/ParameterSet.");
                if(owner.Key.Kind==StrategyCatalogKind.ParameterSchema) { _=StrategyCatalogValidation.ReadShape(owner.Settings);return; }
                StrategyCatalogValidation.ValidateParameters(StrategyCatalogValidation.ReadShape(dependencies[owner.Parent!].Definition.Settings),owner.Settings);
                // Publication validates a complete supported rule matrix. Binding additionally constrains it to the pinned common policy.
                _=TradeSelectionPolicy.ReadSpecialized(owner.Settings.GetRawText(),TradeSelectionDefaultProfiles.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"),Domain.MarketData.Analytics.Shared.TimeFrameType.Daily));
                return;
            }
            if(capability.Role is "evaluator" or "data")
            {
                Require(owner.Key.Kind==StrategyCatalogKind.Strategy && owner.Structures.Length>0,"TS.CONFIG.CAPABILITY_UNSUPPORTED","Selector capability requires a strategy with exact structures.");return;
            }
            if(owner.Key.Kind==StrategyCatalogKind.Deployment)
            {
                foreach(var key in owner.Variants)
                {
                    var variant=dependencies[key];ValidateVariant(SelectionCatalogTransport.From(variant),SelectionCatalogTransport.From(dependencies[variant.Definition.Parent!]));
                }
                return;
            }
            Require(owner.Key.Kind==StrategyCatalogKind.Variant,"TS.CONFIG.CAPABILITY_UNSUPPORTED","StructureVariant validates variant definitions only.");
            ValidateVariant(SelectionCatalogTransport.From(new StoredStrategyCatalogDefinition(owner,StrategyCatalogValidation.ContentHash(owner),CatalogLifecycleStatus.Published,DateTime.UnixEpoch,"validation",DateTime.UnixEpoch,"validation",null,null)),SelectionCatalogTransport.From(dependencies[owner.Parent!]));
        }
    }
    public static void ValidateVariant(SelectionCatalogDefinitionSnapshot variant,SelectionCatalogDefinitionSnapshot structure)
    {
        var builder=structure.Capabilities.SingleOrDefault(x=>x.Role=="builder")??throw new TradeSelectionValidationException("TS.CONFIG.CAPABILITY_UNSUPPORTED","Missing unique builder requirement.");
        var rule=TradeSelectionDefaultProfiles.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"),Domain.MarketData.Analytics.Shared.TimeFrameType.Daily).VariantRules
            .SingleOrDefault(x=>x.BuilderCapabilityCode==builder.Code && x.BuilderCapabilityVersion==builder.Version && x.Side==variant.Side && x.Bias==variant.Bias && x.PremiumMode==variant.PremiumMode);
        Require(rule is not null,"TS.CONFIG.CAPABILITY_UNSUPPORTED","Unsupported variant signature.");
        var legs=structure.Legs.ToDictionary(x=>x.Key);
        Require(structure.ExpiryGroups.Length==1 && structure.ExpiryGroups[0].AfterGroup is null && structure.Legs.All(x=>x.Ratio==1)
            && variant.VariantLegs.Length==legs.Count && variant.VariantLegs.All(x=>x.Ratio==1 && legs.ContainsKey(x.LegKey)),"TS.CONFIG.VARIANT","V1 requires complete unit-ratio same-expiry legs.");
        Dictionary<string,(string Right,string Side)> required=builder.Code switch
        {
            "Future"=>new(){["Future"]=("None",variant.Side=="Long"?"Buy":"Sell")},
            "CallVertical"=>new(){["Lower"]=("Call",variant.Side=="Long"?"Buy":"Sell"),["Upper"]=("Call",variant.Side=="Long"?"Sell":"Buy")},
            "PutVertical"=>new(){["Lower"]=("Put",variant.Side=="Long"?"Sell":"Buy"),["Upper"]=("Put",variant.Side=="Long"?"Buy":"Sell")},
            "IronCondor"=>new(){["LowerPut"]=("Put",variant.Side=="Short"?"Buy":"Sell"),["UpperPut"]=("Put",variant.Side=="Short"?"Sell":"Buy"),["LowerCall"]=("Call",variant.Side=="Short"?"Sell":"Buy"),["UpperCall"]=("Call",variant.Side=="Short"?"Buy":"Sell")},
            _=>throw new TradeSelectionValidationException("TS.CONFIG.VARIANT","Unsupported builder.")
        };
        Require(required.Count==legs.Count && required.All(x=>legs.TryGetValue(x.Key,out var l) && l.OptionRight==x.Value.Right && l.InstrumentClass==(builder.Code=="Future"?"Futures":"FuturesOption")
            && variant.VariantLegs.Single(v=>v.LegKey==x.Key).Side==x.Value.Side),"TS.CONFIG.VARIANT","Variant topology does not match its side and premium mode.");
        TradeSelectionPolicy.CheckJson(variant.SettingsJson);using var json=JsonDocument.Parse(variant.SettingsJson);var settings=json.RootElement;
        string[] fields=["TargetNetDelta","BalanceTolerance","SymmetricWings","MinimumWingWidth","MaximumWingWidth","DeltaUnits"];
        Require(settings.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(fields),"TS.CONFIG.VARIANT","Complete explicit variant settings and delta units are required.");
        var delta=settings.GetProperty("TargetNetDelta").GetDecimal();var tolerance=settings.GetProperty("BalanceTolerance").GetDecimal();var min=settings.GetProperty("MinimumWingWidth").GetDecimal();var max=settings.GetProperty("MaximumWingWidth").GetDecimal();
        Require(settings.GetProperty("DeltaUnits").GetString()=="UnderlyingEquivalent" && tolerance is >=0 and <=1 && delta is >=-1 and <=1
            && (variant.Bias=="Balanced"?delta==0:variant.Bias=="Bullish"?delta>0:delta<0),"TS.CONFIG.VARIANT","Invalid delta intent or units.");
        Require(builder.Code=="Future"?Math.Abs(delta)==1 && min==0 && max==0:min>0 && max>=min,"TS.CONFIG.VARIANT","Unfinished width/delta placeholders cannot execute.");
        _=settings.GetProperty("SymmetricWings").GetBoolean();
    }
}
