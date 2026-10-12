using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.StrategyCatalog.Contracts;

namespace TomasAI.IFM.Application.Strategy.StrategyCatalog;
/// <summary>Resolves trusted strategy validators registered by application composition.</summary>
public sealed class StrategyCatalogCapabilityRegistry : IStrategyCatalogCapabilities
{
    readonly FrozenDictionary<CatalogCapability, IStrategyCatalogCapabilityValidator> validators;
    /// <summary>Builds an immutable registry of the implemented strategy capabilities.</summary>
    /// <param name="validators">The validators supplied by the owning strategy services.</param>
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

    /// <summary>Validates a catalog definition using its exact registered capability.</summary>
    /// <param name="capability">The required capability identity.</param>
    /// <param name="owner">The definition requesting the capability.</param>
    /// <param name="dependencies">The resolved dependency graph.</param>
    public void Validate(CatalogCapability capability, StrategyCatalogDefinition owner, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (!validators.TryGetValue(capability, out var validator))
            throw new InvalidOperationException($"Unsupported strategy catalog capability: {capability.Role}/{capability.Code}@{capability.Version}.");
        validator.Validate(owner, dependencies);
    }
}
