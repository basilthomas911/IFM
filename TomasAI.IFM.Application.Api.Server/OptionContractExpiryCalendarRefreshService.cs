using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using System.Diagnostics;
using TomasAI.IFM.Application.Storage;

namespace TomasAI.IFM.Application.Api.Server;

public sealed record OptionContractExpiryCalendarOptions
{
    public string[] Symbols { get; init; } = ["ES"];
    public int MaximumProviderConcurrency { get; init; } = 2;
    public int MaximumVerificationConcurrency { get; init; } = 4;
    public bool EnableDiagnosticWindowBenchmark { get; init; }

    public OptionContractExpiryCalendarOptions Validate()
    {
        if (MaximumProviderConcurrency is < 1 or > 16)
            throw new InvalidOperationException(
                $"{nameof(MaximumProviderConcurrency)} must be between 1 and 16.");
        if (MaximumVerificationConcurrency is < 1 or > 16)
            throw new InvalidOperationException(
                $"{nameof(MaximumVerificationConcurrency)} must be between 1 and 16.");
        return this;
    }
}

internal static class OptionRefreshAlgorithms
{
    internal static string FindUnderlyingContractId(
        IReadOnlyList<FuturesContractV3ReadModel> orderedFutures,
        DateOnly expiry)
    {
        ArgumentNullException.ThrowIfNull(orderedFutures);
        if (orderedFutures.Count == 0)
            throw new ArgumentException("At least one futures contract is required.", nameof(orderedFutures));
        var low = 0;
        var high = orderedFutures.Count - 1;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (orderedFutures[middle].LastTradeDate < expiry)
                low = middle + 1;
            else
                high = middle;
        }
        return orderedFutures[low].LastTradeDate >= expiry
            ? orderedFutures[low].ContractId
            : orderedFutures[^1].ContractId;
    }
}

public sealed class OptionContractExpiryCalendarRefreshService(
    IMarketDataApi marketData,
    IInstrumentDefinitionStore instrumentDefinitions,
    ISecuritiesDbContext securities,
    IDbContextFactory dbFactory,
    IFuturesMarketSessionAuthority marketSession,
    OptionContractExpiryCalendarOptions options,
    TimeProvider timeProvider,
    ILogger<OptionContractExpiryCalendarRefreshService> logger)
{
    readonly Lock _refreshGateLock = new();
    readonly Dictionary<string, RefreshGate> _refreshGates = new(StringComparer.Ordinal);

    public async Task<int> RefreshAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        symbol = symbol.Trim().ToUpperInvariant();
        RefreshGate refreshGate;
        lock (_refreshGateLock)
        {
            if (!_refreshGates.TryGetValue(symbol, out refreshGate!))
            {
                refreshGate = new RefreshGate();
                _refreshGates.Add(symbol, refreshGate);
            }
            refreshGate.ReferenceCount++;
        }
        var enteredRefreshGate = false;
        try
        {
            await refreshGate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            enteredRefreshGate = true;
            var totalTimer = Stopwatch.StartNew();
            var valueDate = marketSession.Current.OperationalValueDate;
            var existingFutures = (await securities.GetFuturesContractsBySymbolAsync(symbol, cancellationToken)
                    .ConfigureAwait(false))
                .Where(contract => contract.LastTradeDate >= valueDate)
                .OrderBy(contract => contract.LastTradeDate)
                .ToArray();
            if (existingFutures.Length == 0)
                throw new InvalidOperationException($"No current or future IFM futures contracts exist for '{symbol}'.");
            var storedFutures = await StoredOptionDefinitionRangeLoader.LoadFuturesAsync(
                instrumentDefinitions, symbol, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            var futuresByMaturity = existingFutures.ToDictionary(static future => future.LastTradeDate);
            foreach (var storedFuture in storedFutures.Where(future => future.LastTradeDate >= valueDate))
            {
                if (futuresByMaturity.TryGetValue(storedFuture.LastTradeDate, out var existing))
                {
                    var enriched = existing with
                    {
                        Dataset = storedFuture.Dataset,
                        PublisherId = storedFuture.PublisherId,
                        InstrumentId = storedFuture.InstrumentId,
                        RawSymbol = storedFuture.RawSymbol,
                        DefinitionTimestampUtc = storedFuture.DefinitionTimestampUtc,
                        DefinitionDigest = storedFuture.DefinitionDigest,
                        RawDefinitionReference = storedFuture.RawDefinitionReference,
                        ExpirationUtc = storedFuture.ExpirationUtc,
                        LastTradingUtc = storedFuture.LastTradingUtc,
                        MultiplierValue = storedFuture.MultiplierValue,
                        PriceScale = storedFuture.PriceScale,
                        TickSize = storedFuture.TickSize,
                        MappingVersion = storedFuture.MappingVersion
                    };
                    futuresByMaturity[storedFuture.LastTradeDate] = enriched;
                    if (!existing.OnTheRun && existing.Rollover)
                        await securities.InsertFuturesContractAsync(enriched with { Rollover = false })
                            .ConfigureAwait(false);
                    continue;
                }
                await securities.InsertFuturesContractAsync(storedFuture).ConfigureAwait(false);
                futuresByMaturity.Add(storedFuture.LastTradeDate, storedFuture);
            }
            var futures = futuresByMaturity.Values.OrderBy(static future => future.LastTradeDate).ToArray();
            if (futures.Length < 2)
                throw new InvalidOperationException(
                    $"The authoritative stored definition snapshot contains fewer than two active '{symbol}' futures contracts.");
            var horizon = OptionExpiryCalendarPolicy.CalculateCoverageThrough(valueDate, futures);

            var cached = new List<CachedOptionContractDefinitionReadModel>();
            var providerTimer = Stopwatch.StartNew();
            var expiryMappings = new HashSet<(DateOnly Expiry, string Root, string Underlying)>(
                EqualityComparer<(DateOnly, string, string)>.Default);
            var rootRequests = new List<(string Family, string Root)>();
            foreach (var (family, roots) in OptionExpiryCalendarPolicy.GetRoots(symbol))
                foreach (var root in roots)
                    rootRequests.Add((family, root));
            var definitionsByRoot = new FuturesOptionContractReadModel[rootRequests.Count][];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, rootRequests.Count),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = options.MaximumProviderConcurrency
                },
                async (index, token) =>
                {
                    var root = rootRequests[index].Root;
                    try
                    {
                        definitionsByRoot[index] = await LoadRootAsync(
                            symbol, root, valueDate, horizon, futures, token).ConfigureAwait(false);
                    }
                    catch (DatabentoFeedException exception) when (
                        exception.Message.Contains(
                            "Could not resolve smart symbols",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation(
                            "{Component}.{Method} "+"Databento option root {Root}.OPT is not listed for the active horizon; skipping it.",nameof(OptionContractExpiryCalendarRefreshService),nameof(RefreshAsync),                            root);
                        definitionsByRoot[index] = [];
                    }
                }).ConfigureAwait(false);
            var refreshedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            for (var index = 0; index < rootRequests.Count; index++)
            {
                var (family, root) = rootRequests[index];
                foreach (var definition in definitionsByRoot[index])
                {
                    var expiry = definition.ExpirationUtc is { } timestamp
                        ? DateOnly.FromDateTime(timestamp.UtcDateTime) : definition.ContractMonth;
                    var underlyingId = definition.UnderlyingContractId
                        ?? OptionRefreshAlgorithms.FindUnderlyingContractId(futures, expiry);
                    expiryMappings.Add((expiry, root, underlyingId));
                    cached.Add(new()
                    {
                        Symbol = symbol,
                        UnderlyingContractId = underlyingId,
                        ExpiryDate = expiry,
                        ProviderRoot = root,
                        OptionFamily = family,
                        Definition = definition,
                        RefreshedAtUtc = refreshedAtUtc
                    });
                }
            }
            if (cached.Count == 0)
                throw new InvalidOperationException($"Databento returned no cacheable option definitions for '{symbol}'.");
            providerTimer.Stop();
            var saveTimer = Stopwatch.StartNew();
            await securities.ReplaceOptionContractDefinitionsAsync(
                symbol, valueDate, horizon, cached, cancellationToken).ConfigureAwait(false);
            saveTimer.Stop();
            var verificationTimer = Stopwatch.StartNew();
            await VerifyPublishedCacheAsync(symbol, valueDate, horizon, cached, cancellationToken)
                .ConfigureAwait(false);
            verificationTimer.Stop();
            logger.LogInformation(
                "{Component}.{Method} "+"Option cache benchmark phases {Symbol}: Databento={ProviderMs:F1} ms; ScyllaSave={SaveMs:F1} ms; ReadBack={ReadMs:F1} ms.",nameof(OptionContractExpiryCalendarRefreshService),nameof(RefreshAsync),                symbol,providerTimer.Elapsed.TotalMilliseconds,saveTimer.Elapsed.TotalMilliseconds,                verificationTimer.Elapsed.TotalMilliseconds);
            totalTimer.Stop();
            if (options.EnableDiagnosticWindowBenchmark)
            {
                var window = await BenchmarkWindowAsync(symbol, valueDate, cached, cancellationToken)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "{Component}.{Method} "+"Option cache diagnostic {Symbol}: WindowQuery={WindowMs:F1} ms; WindowContracts={WindowContracts}; WindowStrikes={WindowStrikes}; Underlying={Underlying}; StdDev={StdDev}.",nameof(OptionContractExpiryCalendarRefreshService),nameof(RefreshAsync),                    symbol,window.Elapsed.TotalMilliseconds,window.ContractCount,                    window.StrikeCount,window.UnderlyingPrice,window.StandardDeviationAmount);
            }
            logger.LogInformation(
                "{Component}.{Method} "+"Published {DefinitionCount} option definitions across {ExpiryCount} expiry mappings for {Symbol}, coverage {From:yyyy-MM-dd} through {Through:yyyy-MM-dd}, in {TotalMs:F1} ms.",nameof(OptionContractExpiryCalendarRefreshService),nameof(RefreshAsync),                cached.Count,expiryMappings.Count,symbol,valueDate,horizon,                totalTimer.Elapsed.TotalMilliseconds);
            return cached.Count;
        }
        finally
        {
            if (enteredRefreshGate)
                refreshGate.Semaphore.Release();
            lock (_refreshGateLock)
            {
                refreshGate.ReferenceCount--;
                if (refreshGate.ReferenceCount == 0)
                {
                    _refreshGates.Remove(symbol);
                    refreshGate.Semaphore.Dispose();
                }
            }
        }
    }

    sealed class RefreshGate
    {
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);
        internal int ReferenceCount { get; set; }
    }

    async Task<(TimeSpan Elapsed, int ContractCount, int StrikeCount, decimal UnderlyingPrice,
        decimal StandardDeviationAmount)> BenchmarkWindowAsync(string symbol, DateOnly valueDate,
        IReadOnlyCollection<CachedOptionContractDefinitionReadModel> expected,
        CancellationToken cancellationToken)
    {
        var first = expected.OrderBy(row => row.ExpiryDate).ThenBy(row => row.ProviderRoot, StringComparer.Ordinal)
            .First();
        var eod = await dbFactory.MarketDataDb.GetFuturesEodDataAsync(first.UnderlyingContractId, valueDate)
            .ConfigureAwait(false);
        if (eod is not { ClosePrice: > 0, DailyStdDevAmount: > 0 })
            eod = await dbFactory.MarketDataDb.GetLastFuturesEodDataAsync(first.UnderlyingContractId, valueDate)
                .ConfigureAwait(false);
        var livePrice = await marketData.GetFuturesPriceAsync(first.UnderlyingContractId).ConfigureAwait(false);
        var useDesignInput = livePrice is not > 0 || eod is not { DailyStdDevAmount: > 0 };
        var price = useDesignInput ? 5420.50m : livePrice!.Value;
        var deviation = useDesignInput ? 45m : (decimal)eod!.DailyStdDevAmount;
        if (useDesignInput)
            logger.LogWarning("{Component}.{Method} "+"No current futures price and EOD Bollinger deviation are both available for {ContractId}; window timing uses the documented design benchmark input price 5420.50 and sigma 45.00.",nameof(OptionContractExpiryCalendarRefreshService),nameof(BenchmarkWindowAsync),first.UnderlyingContractId);
        var lower = price - 2.5m * deviation;
        var upper = price + 2.5m * deviation;
        var timer = Stopwatch.StartNew();
        var rows = await securities.GetCachedOptionContractDefinitionsAsync(symbol,
                first.UnderlyingContractId, first.ExpiryDate, [first.ProviderRoot], cancellationToken)
            .ConfigureAwait(false);
        var selected = rows.Where(row => row.Definition.GetExactStrikePrice() >= lower
                                         && row.Definition.GetExactStrikePrice() <= upper)
            .OrderBy(row => Math.Abs(row.Definition.GetExactStrikePrice() - price))
            .Take(80).ToArray();
        timer.Stop();
        return (timer.Elapsed, selected.Length,
            selected.Select(row => row.Definition.GetExactStrikePrice()).Distinct().Count(), price, deviation);
    }

    async Task<FuturesOptionContractReadModel[]> LoadRootAsync(string symbol, string root,
        DateOnly from, DateOnly through, IReadOnlyList<FuturesContractV3ReadModel> futures,
        CancellationToken cancellationToken) =>
        await StoredOptionDefinitionRangeLoader.LoadAsync(
            instrumentDefinitions, symbol, root, from, through, futures,
            timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

    async Task VerifyPublishedCacheAsync(string symbol, DateOnly from, DateOnly through,
        IReadOnlyCollection<CachedOptionContractDefinitionReadModel> expected,
        CancellationToken cancellationToken)
    {
        var expectedGroups = expected
            .GroupBy(row => (row.UnderlyingContractId, row.ExpiryDate, Root: row.ProviderRoot.ToUpperInvariant()))
            .ToArray();
        var expiries = await securities.GetOptionContractExpiriesAsync(symbol, from, through, cancellationToken)
            .ConfigureAwait(false);
        var actualMappings = expiries
            .Select(row => (UnderlyingContractId: row.ContractId, row.ExpiryDate,
                Root: row.ProviderRoot.ToUpperInvariant()))
            .ToHashSet();
        var missingMappings = expectedGroups.Select(group => group.Key)
            .Where(key => !actualMappings.Contains(key)).ToArray();
        if (missingMappings.Length > 0)
            throw new InvalidDataException($"Published option cache is missing {missingMappings.Length} expiry mappings.");

        var readCount = 0;
        await Parallel.ForEachAsync(
            expectedGroups,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.MaximumVerificationConcurrency
            },
            async (group, token) =>
            {
                var rows = await securities.GetCachedOptionContractDefinitionsAsync(
                    symbol,
                    group.Key.UnderlyingContractId,
                    group.Key.ExpiryDate,
                    [group.Key.Root],
                    token).ConfigureAwait(false);
                var expectedIds = group.Select(row => row.Definition.ContractId)
                    .ToHashSet(StringComparer.Ordinal);
                var actualIds = rows.Select(row => row.Definition.ContractId)
                    .ToHashSet(StringComparer.Ordinal);
                if (!expectedIds.SetEquals(actualIds))
                    throw new InvalidDataException(
                        $"Published option cache read-back differs for {group.Key.Root} {group.Key.ExpiryDate:yyyy-MM-dd}: expected {expectedIds.Count}, read {actualIds.Count}.");
                Interlocked.Add(ref readCount, rows.Count);
            }).ConfigureAwait(false);
        logger.LogInformation(
            "{Component}.{Method} "+"Verified {DefinitionCount} cached option definitions by deserializing {MappingCount} published expiry/root mappings for {Symbol}.",nameof(OptionContractExpiryCalendarRefreshService),nameof(VerifyPublishedCacheAsync),            readCount,expectedGroups.Length,symbol);
    }
}

public sealed class OptionContractExpiryCalendarStartupService(
    IApplicationStartupStatusStore startupStatus,
    OptionContractExpiryCalendarRefreshService refresh,
    OptionContractExpiryCalendarOptions options,
    ILogger<OptionContractExpiryCalendarStartupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var status = startupStatus.Current;
        while (status.State is not (ApplicationLifecycleState.Running or ApplicationLifecycleState.Degraded))
        {
            status = await startupStatus.WaitForChangeAsync(status, stoppingToken).ConfigureAwait(false);
        }

        foreach (var symbol in options.Symbols.Where(symbol => !string.IsNullOrWhiteSpace(symbol)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await refresh.RefreshAsync(symbol, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception,"{Component}.{Method} "+"Option-expiry startup refresh failed for {Symbol}; the prior published cache remains active.",nameof(OptionContractExpiryCalendarStartupService),nameof(ExecuteAsync),symbol);
            }
        }
    }

}
