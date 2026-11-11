using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Reads a single published generation and caches only its immutable expiry partitions.</summary>
public sealed class SecuritiesOptionDefinitionReader(ISecuritiesDbReadContext source, TimeProvider? clock = null)
{
    readonly AsyncReadCache<(string Symbol, Guid Generation, DateOnly Expiry), CachedOptionContractDefinitionReadModel[]> definitionPartitions =
        new(64, TimeSpan.FromMinutes(10), clock);

    /// <summary>Reads a reviewed definition partition for the supplied underlying and expiry.</summary>
    public async Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> GetOptionDefinitionsAsync(
        string symbol, string underlyingContractId, DateOnly expiryDate,
        IReadOnlyCollection<string>? providerRoots = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingContractId);
        symbol = symbol.Trim().ToUpperInvariant();
        var state = await source.GetOptionDefinitionGenerationAsync(symbol, cancellationToken)
            .ConfigureAwait(false);
        if (state is null || expiryDate < state.CoverageFrom || expiryDate > state.CoverageThrough) return [];
        var roots = (providerRoots ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The published generation is checked on every call. Only its immutable payload partition is cached.
        var rows = await definitionPartitions.GetAsync((symbol, state.Generation, expiryDate), async token =>
        {
            var partition = new List<CachedOptionContractDefinitionReadModel>();
            await foreach (var row in source.ReadOptionDefinitionPartitionsAsync(symbol, state.Generation, [expiryDate], token)
                .ConfigureAwait(false))
                partition.Add(row);
            return partition.ToArray();
        }, cancellationToken).ConfigureAwait(false) ?? [];
        return FilterDefinitions(rows, underlyingContractId, roots).ToArray();
    }

    /// <summary>Streams reviewed rows from one generation, reading all uncached expiries in one query.</summary>
    public async IAsyncEnumerable<CachedOptionContractDefinitionReadModel> ReadPublishedOptionDefinitionsAsync(
        string symbol, DateOnly first, DateOnly last, int maximumExpiries,
        IReadOnlyCollection<string>? providerRoots = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpiries);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        symbol = symbol.Trim().ToUpperInvariant();
        var state = await source.GetOptionDefinitionGenerationAsync(symbol, cancellationToken)
            .ConfigureAwait(false);
        if (state is null) yield break;
        var expiries = await source.GetOptionDefinitionExpiriesAsync(symbol, first, last, state, cancellationToken)
            .ConfigureAwait(false);
        if (expiries.Count > maximumExpiries)
            throw new InvalidDataException("Raw option expiry capacity exceeded.");

        var pairs = expiries.Select(row => (row.ExpiryDate, row.ContractId)).Distinct();

        // SQL and the existing calendar reader both order partitions by expiry.
        // Retain the first underlying group order and the established .NET filters.
        var groups = pairs.GroupBy(row => row.ExpiryDate).ToArray();
        var cached = new Dictionary<DateOnly, CachedOptionContractDefinitionReadModel[]>();
        foreach (var group in groups)
            if (definitionPartitions.TryGetCompleted((symbol, state.Generation, group.Key), out var partition))
                cached.Add(group.Key, partition!);
        var missing = groups.Select(group => group.Key).Where(expiry => !cached.ContainsKey(expiry)).ToArray();
        var roots = (providerRoots ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await using var cold = source.ReadOptionDefinitionPartitionsAsync(symbol, state.Generation, missing, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        var more = await cold.MoveNextAsync()
            .ConfigureAwait(false);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!cached.TryGetValue(group.Key, out var rows))
            {
                var partition = new List<CachedOptionContractDefinitionReadModel>();
                while (more && cold.Current.ExpiryDate == group.Key)
                {
                    partition.Add(cold.Current);
                    more = await cold.MoveNextAsync()
            .ConfigureAwait(false);
                }
                rows = partition.ToArray();
                definitionPartitions.RememberCompleted((symbol, state.Generation, group.Key), rows);
            }
            foreach (var (_, underlying) in group)
                foreach (var row in FilterDefinitions(rows, underlying, roots))
                    yield return row;
        }
    }

    static IEnumerable<CachedOptionContractDefinitionReadModel> FilterDefinitions(
        IEnumerable<CachedOptionContractDefinitionReadModel> rows, string underlyingContractId, HashSet<string> roots)
        => rows.Where(row => string.Equals(row.UnderlyingContractId, underlyingContractId, StringComparison.OrdinalIgnoreCase)
            && (roots.Count == 0 || roots.Contains(row.ProviderRoot))).DistinctBy(row => row.Definition.ContractId);
}
