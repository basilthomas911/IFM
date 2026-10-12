using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Trusted server-side capability registry. Implementations validate semantics as well as availability.</summary>
public interface IStrategyCatalogCapabilities
{
    void Validate(CatalogCapability capability, StrategyCatalogDefinition owner,
        IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies);
}
