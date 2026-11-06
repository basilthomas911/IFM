using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

/// <summary>A read must return the exact immutable reviewed mapping version; there is no latest fallback.</summary>
public interface IOptionPricingConventionStore
{
    Task<OptionPricingConvention?> GetAsync(string contractId, string mappingVersion, CancellationToken cancellationToken);

    /// <summary>Resolves exact versions in request order, including missing and repeated entries.</summary>
    async Task<IReadOnlyList<OptionPricingConvention?>> GetManyAsync(
        IReadOnlyList<(string ContractId, string Version)> identities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identities);
        var result = new OptionPricingConvention?[identities.Count];
        for (var index = 0; index < identities.Count; index++)
        {
            var (id, version) = identities[index];
            result[index] = await GetAsync(id, version, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }
}
