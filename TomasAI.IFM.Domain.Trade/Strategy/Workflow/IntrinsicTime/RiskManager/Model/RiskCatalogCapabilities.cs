using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Publication capabilities for structures implemented by RiskUnitModel. Runtime pricing, policy and financial authority remain mandatory.</summary>
public static class RiskCatalogCapabilities
{
    public static IStrategyCatalogCapabilityValidator[] Create()=>
        [new Validator("Future",1),new Validator("CallVertical",2),new Validator("PutVertical",2),new Validator("IronCondor",4)];

    sealed class Validator(string code,int legs):IStrategyCatalogCapabilityValidator
    {
        public CatalogCapability Capability=>new("risk",code,1);
        public void Validate(StrategyCatalogDefinition owner,IReadOnlyDictionary<CatalogKey,StoredStrategyCatalogDefinition> dependencies)
        {
            if(owner.Key.Kind!=StrategyCatalogKind.Structure || owner.Legs.Length!=legs || owner.ExpiryGroups.Length!=1 ||
                owner.Legs.Any(x=>x.Ratio!=1) || !owner.Capabilities.Contains(new CatalogCapability("builder",code,1)))
                throw new ArgumentException("RM.CONFIG.UNSUPPORTED_STRUCTURE");
        }
    }
}
