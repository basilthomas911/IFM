using System.Collections.Frozen;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Definitions;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Application contract mapping over native parsed/query-owned
/// definitions. No managed provider driver or legacy native DLL is used.</summary>
public sealed class NativeContractCatalog : IDatabentoMarketDataCatalog
{
    readonly NativeMarketDataRuntime runtime;
    readonly TimeProvider clock;
    readonly Dictionary<string, Resolved> resolved = new(StringComparer.Ordinal);
    readonly Dictionary<(string Dataset, string Provider), string> underlyingIds = [];
    readonly TimeSpan queryBudget;
    readonly FrozenDictionary<string, NativeContractDefinition> definitions;
    sealed record Resolved(NativeContractDefinition Detail, FuturesContractV3ReadModel? Future, FuturesOptionContractReadModel? Option);

    /// <summary>Builds the configured epoch catalog through Rust-owned exact
    /// provider queries. All datasets consume one original bootstrap budget.
    /// Query snapshots are released on every exit, including partial failure;
    /// no live subscription or managed provider driver is constructed.</summary>
    public static async Task<NativeContractCatalog> CreateAsync(NativeMarketDataRuntime runtime,
        DatabentoMarketDataRuntimeOptions options, TimeProvider clock, TimeSpan remainingBudget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(clock);
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if (remainingBudget > TimeSpan.FromMinutes(2)) remainingBudget = TimeSpan.FromMinutes(2);
        cancellationToken.ThrowIfCancellationRequested();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var registrations = options.Contracts.ToArray();
        if (registrations.Length is < 1 or > 2048) throw new ArgumentOutOfRangeException(nameof(options));
        // Freeze configured registrations for the complete bootstrap.
        options = options with { Contracts = registrations };
        var groups = registrations.GroupBy(r => string.IsNullOrWhiteSpace(r.Dataset) ? options.FeedOptions.Dataset : r.Dataset, StringComparer.Ordinal).ToArray();
        if (groups.Length > 3) throw new ArgumentException("The configured native epoch supports at most three datasets.", nameof(options));
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var queries = new List<(string Dataset, NativeCatalogSnapshot Snapshot)>();
        TimeSpan Remaining()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = remainingBudget - System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("Native catalog original bootstrap deadline expired.");
            return remaining;
        }
        try
        {
            // Sequential scopes bound retained query ownership and concurrency.
            // The same absolute budget covers every dataset and final hydration.
            foreach (var group in groups)
                queries.Add((group.Key!, await runtime.AcquireExactDefinitionSnapshotAsync(group.Key!,
                    group.Select(r => r.ProviderContractName).ToArray(), Remaining(), stopping.Token).ConfigureAwait(false)));
            stopping.CancelAfter(Remaining());
            async IAsyncEnumerable<CatalogSourceLine> Lines([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
            {
                foreach (var query in queries)
                    for (var index = 0; index < query.Snapshot.Count; index++)
                    {
                        token.ThrowIfCancellationRequested();
                        yield return new(query.Dataset, query.Snapshot.ReadSource(index));
                    }
                await Task.CompletedTask.ConfigureAwait(false);
            }
            await runtime.HydrateCatalogAsync(Lines(stopping.Token), stopping.Token).ConfigureAwait(false);
            _ = Remaining();
            var result = new NativeContractCatalog(runtime, options, clock);
            _ = Remaining();
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Native catalog original bootstrap deadline expired.");
        }
        finally
        {
            foreach (var query in queries) query.Snapshot.Dispose();
        }
    }

    /// <summary>Maps the configured registrations from the native bootstrap
    /// catalog. Provider identities must resolve uniquely before starting feeds.</summary>
    public NativeContractCatalog(NativeMarketDataRuntime runtime, DatabentoMarketDataRuntimeOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(clock);
        if (options.Contracts.Count == 0 || options.Contracts.Select(r => r.DomainContractId).Distinct(StringComparer.Ordinal).Count() != options.Contracts.Count)
            throw new ArgumentException("Configured domain contract IDs must be nonempty and unique.", nameof(options));
        this.runtime = runtime; this.clock = clock; queryBudget = TimeSpan.FromMinutes(2);
        using var snapshot = runtime.OpenCatalogSnapshot();
        var rows = new NativeContractDefinition[snapshot.Count];
        for (var index = 0; index < rows.Length; index++) rows[index] = snapshot.ReadDefinition(index);
        var providerIdentities = new HashSet<(string Dataset, string Symbol)>();
        foreach (var registration in options.Contracts)
        {
            var dataset = string.IsNullOrWhiteSpace(registration.Dataset) ? options.FeedOptions.Dataset : registration.Dataset;
            // The preserved bridge's exact query consumes each raw-symbol entry
            // once. A repeated configured provider symbol therefore yields a
            // missing second result, not a second independently owned contract.
            if (!providerIdentities.Add((dataset!, registration.ProviderContractName)))
                throw new MarketDataContractNotFoundException(registration.DomainContractId);
            var candidates = rows.Where(d => d.Dataset == dataset && d.Symbol == registration.ProviderContractName).Take(2).ToArray();
            if (candidates.Length == 0) throw new MarketDataContractNotFoundException(registration.DomainContractId);
            if (candidates.Length != 1) throw new MarketDataContractMappingException(registration.DomainContractId, "provider identity is ambiguous");
            var detail = candidates[0]; ValidateQueryDetail(detail);
            var expectedFuture = registration.AssetTypeId == AssetTypeId.Futures;
            if (expectedFuture ? detail.Kind != 1 : registration.AssetTypeId != AssetTypeId.FuturesOption || detail.Kind is not (2 or 3))
                throw new MarketDataContractKindMismatchException(registration.DomainContractId, registration.AssetTypeId.ToString(), ((ContractKind)detail.Kind).ToString());
            options.FuturesContractCurrencyFallbacks.TryGetValue(detail.Asset, out var fallback);
            if (expectedFuture)
            {
                using var source=runtime.OpenCatalogSnapshot(dataset);
                var future=source.ReadConfiguredFuture(registration.DomainContractId,detail.Symbol,fallback,registration.OnTheRun,registration.Rollover);
                resolved.Add(registration.DomainContractId, new(detail, future, null));
                underlyingIds.TryAdd((dataset!, registration.ProviderContractName), registration.DomainContractId);
                underlyingIds.TryAdd((dataset!, detail.Symbol), registration.DomainContractId);
            }
            else
            {
                using var source=runtime.OpenCatalogSnapshot(dataset);
                var models=source.ReadOptionModels(fallback,[],configuredId:registration.DomainContractId,sourceSymbol:detail.Symbol);
                if(models.Length!=1)throw new InvalidDataException("Configured native option model is missing.");
                resolved.Add(registration.DomainContractId,new(detail,null,models[0]));
            }
        }
        definitions = resolved.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.Detail, StringComparer.Ordinal);
    }

    /// <summary>Configured immutable native definitions used to realize provider lanes.</summary>
    public IReadOnlyDictionary<string, NativeContractDefinition> Definitions => definitions;
    /// <inheritdoc/>
    public bool TryGetMarketInstrumentId(string contractId, out uint marketInstrumentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (resolved.TryGetValue(contractId, out var value)) { marketInstrumentId = value.Detail.Instrument; return true; }
        marketInstrumentId = 0; return false;
    }
    /// <inheritdoc/>
    public FuturesContractV3ReadModel? FindFutures(string contractId) => resolved.GetValueOrDefault(contractId)?.Future;
    /// <inheritdoc/>
    public FuturesOptionContractReadModel? FindFuturesOption(string contractId) => resolved.GetValueOrDefault(contractId)?.Option;
    /// <inheritdoc/>
    public string? FindOptionUnderlying(string id) => resolved.TryGetValue(id, out var row) && row.Option is not null ? underlyingIds.GetValueOrDefault((row.Detail.Dataset, row.Detail.Underlying)) : null;

    Resolved Underlying(string symbol) => resolved.Values.FirstOrDefault(r => r.Future is not null &&
        (string.Equals(r.Future.Symbol, symbol, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Detail.Asset, symbol, StringComparison.OrdinalIgnoreCase)))
        ?? throw new MarketDataContractNotFoundException(symbol);
    static void ValidateQueryDetail(NativeContractDefinition detail)
    {
        if (detail.QueryMaturityInvalid) throw new DatabentoFeedException(DatabentoFeedStatus.AbiMismatch, "A native contract detail marked an invalid maturity date as present.");
    }
    NativeOptionModelUnderlying[] UnderlyingMappings(string dataset)=>underlyingIds
        .Where(pair=>string.Equals(pair.Key.Dataset,dataset,StringComparison.Ordinal))
        .Select(pair=>new NativeOptionModelUnderlying(pair.Key.Provider,pair.Value,resolved[pair.Value].Detail.Publisher)).ToArray();
    NativeContractDefinition[] Read(NativeCatalogSnapshot snapshot)
    {
        var rows = new NativeContractDefinition[snapshot.Count];
        for (var i = 0; i < rows.Length; i++) { rows[i] = snapshot.ReadDefinition(i); ValidateQueryDetail(rows[i]); }
        return rows;
    }
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(string futuresContractId, DateOnly maturityDate)
    {
        if (!resolved.TryGetValue(futuresContractId, out var underlying) || underlying.Future is null) throw new MarketDataContractNotFoundException(futuresContractId);
        using var source = await runtime.AcquireDefinitionSnapshotAsync(underlying.Detail.Dataset, [underlying.Detail.Asset + ".OPT"], queryBudget).ConfigureAwait(false);
        _ = Read(source); // Current provider conversion rejects any malformed detail before filtering.
        using var selected = source.SelectChain(maturityDate, underlying.Detail.Symbol, futuresContractId);
        return selected.ReadOptionModels(underlying.Future.Currency,UnderlyingMappings(underlying.Detail.Dataset));
    }
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetOptionChainBySymbolAsync(string symbol, DateOnly maturityDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var underlying = Underlying(symbol);
        using var source = await runtime.AcquireDefinitionSnapshotAsync(underlying.Detail.Dataset, [symbol.Trim().ToUpperInvariant() + ".OPT"], queryBudget).ConfigureAwait(false);
        _ = Read(source);
        using var selected = source.SelectChain(maturityDate, symbol, underlying.Future!.ContractId, policy: 1);
        return selected.ReadOptionModels(underlying.Future.Currency,UnderlyingMappings(underlying.Detail.Dataset));
    }
    /// <inheritdoc/>
    public Task<FuturesOptionContractReadModel[]> GetOptionChainByRootAsync(string symbol, string root, DateOnly maturity) => GetOptionChainByRootAsync(symbol, root, maturity, maturity);
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetOptionChainByRootAsync(string symbol, string root, DateOnly from, DateOnly through, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol); ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfLessThan(through, from);
        var underlying = Underlying(symbol);
        using var source = await runtime.AcquireDefinitionSnapshotAsync(underlying.Detail.Dataset, [root.Trim().ToUpperInvariant() + ".OPT"], queryBudget, cancellationToken).ConfigureAwait(false);
        return source.ReadOptionModels(underlying.Future!.Currency,UnderlyingMappings(underlying.Detail.Dataset),orderByMaturity:true,from:from,through:through);
    }
    /// <inheritdoc/>
    public async Task<OptionContractExpiryReadModel[]> DiscoverOptionContractExpiriesAsync(string symbol, DateOnly from, DateOnly through, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol); ArgumentOutOfRangeException.ThrowIfLessThan(through, from);
        var normalized = symbol.Trim().ToUpperInvariant();
        var futures = resolved.Values.Where(r => r.Future is not null && (string.Equals(r.Future.Symbol, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.Detail.Asset, normalized, StringComparison.OrdinalIgnoreCase))).OrderBy(r => r.Future!.LastTradeDate).ToArray();
        if (futures.Length == 0) throw new MarketDataContractNotFoundException(normalized);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var rows = new List<OptionContractExpiryReadModel>();
        foreach (var (family, roots) in OptionExpiryCalendarPolicy.GetRoots(normalized)) foreach (var root in roots)
        {
            var remaining = queryBudget - System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("Option expiry discovery original deadline expired.");
            NativeCatalogSnapshot source;
            try { source = await runtime.AcquireDefinitionSnapshotAsync(futures[0].Detail.Dataset, [root + ".OPT"], remaining, cancellationToken).ConfigureAwait(false); }
            catch (DatabentoFeedException error) when (error.Message.Contains("Could not resolve smart symbols", StringComparison.OrdinalIgnoreCase)) { continue; }
            using var sourceOwnership = source;
            foreach (var group in Read(source).Where(d => d.Kind is 2 or 3 && d.QueryMaturity >= from && d.QueryMaturity <= through).GroupBy(d => d.QueryMaturity!.Value))
            {
                var representative = group.First();
                var contractId = underlyingIds.GetValueOrDefault((representative.Dataset, representative.Underlying))
                    ?? futures.FirstOrDefault(f => f.Future!.LastTradeDate >= group.Key)?.Future!.ContractId ?? futures[^1].Future!.ContractId;
                rows.Add(new() { Symbol = normalized, ContractId = contractId, ExpiryDate = group.Key, ProviderRoot = root, OptionFamily = family, RefreshedAtUtc = clock.GetUtcNow().UtcDateTime });
            }
        }
        return rows.DistinctBy(r => (r.ExpiryDate, r.ProviderRoot, r.ContractId)).OrderBy(r => r.ExpiryDate).ThenBy(r => r.ProviderRoot, StringComparer.Ordinal).ToArray();
    }
}
