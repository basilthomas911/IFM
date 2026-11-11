using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Caches immutable reviewed reference versions while leaving unpublished or missing versions visible on retry.</summary>
public sealed class SecuritiesReferenceReader(ISecuritiesDbReadContext source, TimeProvider? clock = null) : IOptionPricingConventionStore
{
    readonly AsyncReadCache<(string ContractId, string Version), ReferenceContractVersion> versions =
        new(8192, TimeSpan.FromHours(1), clock);

    /// <summary>Resolves a published reference version by its exact contract and revision.</summary>
    public Task<ReferenceContractVersion?> GetReferenceVersionAsync(string contractId, string version, CancellationToken token)
    {
        ValidateIdentity((contractId, version));
        return versions.GetAsync((contractId, version), ct => source.GetReferenceVersionAsync(new ExactReferenceVersion(contractId, version), ct), token);
    }

    /// <summary>Checks whether the exact published reference covers the requested valuation instant.</summary>
    public async Task<ReferenceContractVersion?> GetEffectiveReferenceVersionAsync(string contractId, string version,
        DateTimeOffset effectiveAtUtc, CancellationToken cancellationToken = default)
    {
        if (effectiveAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Historical valuation time must be UTC.", nameof(effectiveAtUtc));
        var value = await GetReferenceVersionAsync(contractId, version, cancellationToken).ConfigureAwait(false);
        var from = value?.Option?.EffectiveFromUtc ?? value?.Future?.EffectiveFromUtc;
        var until = value?.Option?.EffectiveUntilUtc ?? value?.Future?.EffectiveUntilUtc;
        return from <= effectiveAtUtc && until > effectiveAtUtc ? value : null;
    }

    /// <summary>Gets a qualified page of staged or published version summaries.</summary>
    public Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsAsync(string contractId,
        string afterVersion = "", int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(afterVersion);
        if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128 || afterVersion.Length > 128 ||
            limit is < 1 or > 200)
            throw new ArgumentException("Invalid bounded version-history query.", nameof(contractId));
        return source.ListReferenceVersionsAsync(contractId, afterVersion, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OptionPricingConvention?> GetAsync(string contractId, string mappingVersion, CancellationToken cancellationToken)
        => (await GetReferenceVersionAsync(contractId, mappingVersion, cancellationToken).ConfigureAwait(false))?.Convention;

    /// <inheritdoc />
    public async Task<IReadOnlyList<OptionPricingConvention?>> GetManyAsync(
        IReadOnlyList<(string ContractId, string Version)> identities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identities);
        cancellationToken.ThrowIfCancellationRequested();
        var result = new OptionPricingConvention?[identities.Count];
        HashSet<(string ContractId, string Version)>? missing = null;
        for (var index = 0; index < identities.Count; index++)
        {
            var identity = identities[index];
            ValidateIdentity(identity);
            if (versions.TryGetCompleted(identity, out var cached))
                result[index] = cached?.Convention;
            else
                (missing ??= []).Add(identity);
        }
        if (missing is null) return result;
        var keys = missing.ToArray();
        var fetched = await source.GetReferenceVersionsAsync(keys, cancellationToken).ConfigureAwait(false);
        var found = new Dictionary<(string ContractId, string Version), ReferenceContractVersion>(keys.Length);
        for (var index = 0; index < keys.Length; index++)
            if (fetched[index] is { } published)
            {
                versions.RememberCompleted(keys[index], published);
                found.Add(keys[index], published);
            }
        for (var index = 0; index < identities.Count; index++)
            if (found.TryGetValue(identities[index], out var value))
                result[index] = value.Convention;
        return result;
    }

    static void ValidateIdentity((string ContractId, string Version) identity)
    {
        if (string.IsNullOrWhiteSpace(identity.ContractId) || identity.ContractId.Length > 128
            || string.IsNullOrWhiteSpace(identity.Version) || identity.Version.Length > 128)
            throw new ArgumentException("Exact reference version is required.", nameof(identity));
    }
}
