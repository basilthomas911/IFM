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
    public TimeSpan StartupPollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
}

public sealed class OptionContractExpiryCalendarRefreshService(
    IMarketDataApi marketData,
    ISecuritiesDbContext securities,
    IDbContextFactory dbFactory,
    IFuturesMarketSessionAuthority marketSession,
    ILogger<OptionContractExpiryCalendarRefreshService> logger)
{
    readonly SemaphoreSlim _refreshGate = new(1, 1);

    public async Task<int> RefreshAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        symbol = symbol.Trim().ToUpperInvariant();
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var totalTimer = Stopwatch.StartNew();
            var valueDate = marketSession.Current.OperationalValueDate;
            var futures = (await securities.GetFuturesContractsBySymbolAsync(symbol, cancellationToken)
                    .ConfigureAwait(false))
                .Where(contract => contract.LastTradeDate >= valueDate)
                .OrderBy(contract => contract.LastTradeDate)
                .ToArray();
            if (futures.Length == 0)
                throw new InvalidOperationException($"No current or future IFM futures contracts exist for '{symbol}'.");
            var horizon = OptionExpiryCalendarPolicy.CalculateCoverageThrough(valueDate, futures);

            var cached = new List<CachedOptionContractDefinitionReadModel>();
            var providerTimer = Stopwatch.StartNew();
            var expiryMappings = new HashSet<(DateOnly Expiry, string Root, string Underlying)>(
                EqualityComparer<(DateOnly, string, string)>.Default);
            foreach (var (family, roots) in OptionExpiryCalendarPolicy.GetRoots(symbol))
            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FuturesOptionContractReadModel[] definitions;
                try
                {
                    definitions = await LoadRootAsync(symbol, root, valueDate, horizon, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (DatabentoFeedException exception) when (
                    exception.Message.Contains("Could not resolve smart symbols", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Databento option root {Root}.OPT is not listed for the active horizon; skipping it.", root);
                    continue;
                }
                foreach (var definition in definitions)
                {
                    var expiry = definition.ExpirationUtc is { } timestamp
                        ? DateOnly.FromDateTime(timestamp.UtcDateTime) : definition.ContractMonth;
                    var underlyingId = definition.UnderlyingContractId
                        ?? futures.FirstOrDefault(contract => contract.LastTradeDate >= expiry)?.ContractId
                        ?? futures[^1].ContractId;
                    expiryMappings.Add((expiry, root, underlyingId));
                    cached.Add(new()
                    {
                        Symbol = symbol, UnderlyingContractId = underlyingId,
                        ExpiryDate = expiry, ProviderRoot = root,
                        OptionFamily = family, Definition = definition,
                        RefreshedAtUtc = DateTime.UtcNow
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
                "Option cache benchmark phases {Symbol}: Databento={ProviderMs:F1} ms; ScyllaSave={SaveMs:F1} ms; ReadBack={ReadMs:F1} ms.",
                symbol, providerTimer.Elapsed.TotalMilliseconds, saveTimer.Elapsed.TotalMilliseconds,
                verificationTimer.Elapsed.TotalMilliseconds);
            var window = await BenchmarkWindowAsync(symbol, valueDate, cached, cancellationToken).ConfigureAwait(false);
            totalTimer.Stop();
            logger.LogInformation(
                "Option cache benchmark {Symbol}: Databento={ProviderMs:F1} ms; ScyllaSave={SaveMs:F1} ms; ReadBack={ReadMs:F1} ms; WindowQuery={WindowMs:F1} ms; Total={TotalMs:F1} ms; WindowContracts={WindowContracts}; WindowStrikes={WindowStrikes}; Underlying={Underlying}; StdDev={StdDev}.",
                symbol, providerTimer.Elapsed.TotalMilliseconds, saveTimer.Elapsed.TotalMilliseconds,
                verificationTimer.Elapsed.TotalMilliseconds, window.Elapsed.TotalMilliseconds,
                totalTimer.Elapsed.TotalMilliseconds, window.ContractCount, window.StrikeCount,
                window.UnderlyingPrice, window.StandardDeviationAmount);
            logger.LogInformation(
                "Published {DefinitionCount} option definitions across {ExpiryCount} expiry mappings for {Symbol}, coverage {From:yyyy-MM-dd} through {Through:yyyy-MM-dd}.",
                cached.Count, expiryMappings.Count, symbol, valueDate, horizon);
            return cached.Count;
        }
        finally
        {
            _refreshGate.Release();
        }
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
            logger.LogWarning("No current futures price and EOD Bollinger deviation are both available for {ContractId}; window timing uses the documented design benchmark input price 5420.50 and sigma 45.00.", first.UnderlyingContractId);
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
        DateOnly from, DateOnly through, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await marketData.GetFuturesOptionChainContractsByRootAsync(
                    symbol, root, from, through, cancellationToken).ConfigureAwait(false);
            }
            catch (DatabentoFeedTimeoutException) when (attempt < 3)
            {
                logger.LogWarning("Databento option root {Root}.OPT timed out on attempt {Attempt}; retrying.", root, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

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
        foreach (var group in expectedGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = await securities.GetCachedOptionContractDefinitionsAsync(symbol,
                    group.Key.UnderlyingContractId, group.Key.ExpiryDate, [group.Key.Root], cancellationToken)
                .ConfigureAwait(false);
            var expectedIds = group.Select(row => row.Definition.ContractId).ToHashSet(StringComparer.Ordinal);
            var actualIds = rows.Select(row => row.Definition.ContractId).ToHashSet(StringComparer.Ordinal);
            if (!expectedIds.SetEquals(actualIds))
                throw new InvalidDataException(
                    $"Published option cache read-back differs for {group.Key.Root} {group.Key.ExpiryDate:yyyy-MM-dd}: expected {expectedIds.Count}, read {actualIds.Count}.");
            readCount += rows.Count;
        }
        logger.LogInformation(
            "Verified {DefinitionCount} cached option definitions by deserializing {MappingCount} published expiry/root mappings for {Symbol}.",
            readCount, expectedGroups.Length, symbol);
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
        while (!stoppingToken.IsCancellationRequested)
        {
            var state = startupStatus.Current.State;
            if (state is ApplicationLifecycleState.Running or ApplicationLifecycleState.Degraded)
                break;
            await Task.Delay(options.StartupPollInterval, stoppingToken).ConfigureAwait(false);
        }

        foreach (var symbol in options.Symbols.Where(symbol => !string.IsNullOrWhiteSpace(symbol)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { await refresh.RefreshAsync(symbol, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Option-expiry startup refresh failed for {Symbol}; the prior published cache remains active.", symbol);
            }
        }
    }
}
