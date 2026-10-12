using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Collections.Frozen;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;

/// <summary>Implemented server capability, registered by application composition, never deserialized from a catalog row.</summary>
public interface IStrategyCatalogCapabilityValidator
{
    CatalogCapability Capability { get; }
    void Validate(StrategyCatalogDefinition owner, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies);
}
