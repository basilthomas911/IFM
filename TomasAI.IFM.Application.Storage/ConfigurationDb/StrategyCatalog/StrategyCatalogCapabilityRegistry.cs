using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Collections.Frozen;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;

public sealed class StrategyCatalogCapabilityRegistry : IStrategyCatalogCapabilities
{
    readonly FrozenDictionary<CatalogCapability, IStrategyCatalogCapabilityValidator> validators;

    public StrategyCatalogCapabilityRegistry(IEnumerable<IStrategyCatalogCapabilityValidator> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        this.validators = validators.ToFrozenDictionary(v => v.Capability);
        foreach (var capability in this.validators.Keys)
        {
            StrategyCatalogValidation.Token(capability.Code);
            StrategyCatalogValidation.Require(capability.Version > 0 && capability.Role is "evaluator" or "builder" or "validator" or "risk" or "data", "Invalid registered capability.");
        }
    }

    public void Validate(CatalogCapability capability, StrategyCatalogDefinition owner,
        IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (!validators.TryGetValue(capability, out var validator))
            throw new InvalidOperationException($"Unsupported strategy catalog capability: {capability.Role}/{capability.Code}@{capability.Version}.");
        validator.Validate(owner, dependencies);
    }
}
