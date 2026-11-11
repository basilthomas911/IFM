using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

/// <summary>A read must return the exact immutable reviewed mapping version; there is no latest fallback.</summary>
public interface IOptionPricingConventionStore
{
    /// <summary>Get.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="mappingVersion">The exact immutable reference revision.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<OptionPricingConvention?> GetAsync(string contractId, string mappingVersion, CancellationToken cancellationToken);

    /// <summary>Resolves exact versions in request order, including missing and repeated entries.</summary>
    /// <param name="identities">The contract and immutable reference revision pairs in request order.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
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
