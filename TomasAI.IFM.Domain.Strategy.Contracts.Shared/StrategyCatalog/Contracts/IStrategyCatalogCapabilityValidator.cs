using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Strategy.Contracts.Shared.StrategyCatalog.Contracts;
/// <summary>Implemented server capability, registered by application composition, never deserialized from a catalog row.</summary>
public interface IStrategyCatalogCapabilityValidator
{
    /// <summary>Gets the exact role, code and version supported by this validator.</summary>
    CatalogCapability Capability { get; }

    /// <summary>Validates the owning definition against its resolved catalog dependencies.</summary>
    /// <param name="owner">The definition requesting the capability.</param>
    /// <param name="dependencies">The resolved definitions keyed by exact catalog version.</param>
    void Validate(StrategyCatalogDefinition owner, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies);
}
