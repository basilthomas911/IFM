using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.UnitTests.Harness;

internal sealed class FakeMarketDataEpochFactory(FakeMarketDataCatalog catalog)
    : IDatabentoMarketDataEpochFactory
{
    private static readonly string[] StageNames =
    [
        "ProviderOperations",
        "Publishers",
        "FuturesFeed",
        "TickAggregation",
        "OptionFeed",
        "OptionRoutes"
    ];

    internal List<string> LifecycleLog { get; } = [];
    internal List<FakeMarketDataEpoch> Epochs { get; } = [];
    internal string? BlockStartAtStage { get; set; }
    internal string? FailStartAtStage { get; set; }
    internal TaskCompletionSource StartEntered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseStart { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal int CreateCount { get; private set; }

    internal FakeMarketDataEpoch Create(DateOnly valueDate)
    {
        CreateCount++;
        var stages = StageNames.Select(name => new FakeLifecycleStage(
            name,
            LifecycleLog,
            string.Equals(name, BlockStartAtStage, StringComparison.Ordinal)
                ? (StartEntered, ReleaseStart)
                : null,
            string.Equals(name, FailStartAtStage, StringComparison.Ordinal)));
        var epoch = new FakeMarketDataEpoch(valueDate, catalog, stages, LifecycleLog);
        Epochs.Add(epoch);
        return epoch;
    }

    IDatabentoMarketDataEpoch IDatabentoMarketDataEpochFactory.Create(DateOnly valueDate) =>
        Create(valueDate);
}

internal sealed class FakeMarketDataEpoch :
    IDatabentoMarketDataEpoch,
    IDatabentoLastPriceReaderProvider
{
    private readonly FakeLifecycleStage[] stages;
    private readonly List<string> lifecycleLog;
    private readonly Dictionary<string, FakeFuturesLastPriceReader> futuresReaders =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeFuturesOptionLastPriceReader> optionReaders =
        new(StringComparer.Ordinal);

    internal FakeMarketDataEpoch(
        DateOnly valueDate,
        FakeMarketDataCatalog catalog,
        IEnumerable<FakeLifecycleStage> stages,
        List<string> lifecycleLog)
    {
        ValueDate = valueDate;
        Catalog = catalog;
        this.stages = stages.ToArray();
        this.lifecycleLog = lifecycleLog;
    }

    public DateOnly ValueDate { get; }
    internal FakeMarketDataCatalog Catalog { get; }
    IDatabentoMarketDataCatalog IDatabentoMarketDataEpoch.Catalog => Catalog;
    public IDatabentoLastPriceReaderProvider LastPrices => this;
    internal FakeTickAggregationStatus TickAggregation { get; } = new();
    internal FakeTreasuryCurve TreasuryCurve { get; } = new();
    internal FakeOptionRouteRegistry OptionRoutes { get; } = new();
    internal HashSet<string> ActiveFuturesRoutes { get; } = new(StringComparer.Ordinal);
    internal HashSet<(string ContractId, TickerStreamOwner Owner)> ActiveStreamOwners { get; } = [];
    internal object FuturesRouteSync { get; } = new();
    internal int StopCount { get; private set; }
    internal int DisposeCount { get; private set; }
    internal FuturesMarketPriceSnapshot? LastMarketPrice { get; set; }

    public bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot)
    {
        if (LastMarketPrice is { } current
            && StringComparer.Ordinal.Equals(current.ContractId, contractId))
        {
            snapshot = current;
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot)
    {
        if (TryGetLastTickPrice(contractId, out var price)
            && price.AssetTypeId == AssetTypeId.FuturesOption)
        {
            snapshot = new OptionTickerPriceSnapshot(ToTickerPrice(price), null);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool IsTickDataStreamActive(string contractId)
    {
        lock (FuturesRouteSync)
            return ActiveStreamOwners.Any(registration =>
                StringComparer.Ordinal.Equals(registration.ContractId, contractId));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var stage in stages)
        {
            await stage.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync()
    {
        StopCount++;
        foreach (var stage in stages.Reverse())
        {
            await stage.StopAsync();
        }

        lock (FuturesRouteSync)
        {
            ActiveFuturesRoutes.Clear();
            ActiveStreamOwners.Clear();
        }
        OptionRoutes.Clear();
    }

    public TickAggregationContractStatus GetAggregationStatus(string contractId)
    {
        var status = TickAggregation.GetStatus(contractId);
        return new TickAggregationContractStatus(
            contractId,
            AssetTypeId.Futures,
            status.ServiceRunning,
            status.TickerConfigured,
            status.TickerRunning);
    }

    public DatabentoMarketDataEpochHealth GetHealth() => new(
        ValueDate,
        true,
        TickAggregation.ServiceRunning,
        Catalog.Futures.Count + Catalog.Options.Count,
        futuresReaders.Count + optionReaders.Count,
        true,
        0,
        0,
        0);

    public bool StartFuturesRoute(TickerStreamOwner owner, string futuresContractId)
    {
        lock (FuturesRouteSync)
        {
            var added = ActiveStreamOwners.Add((futuresContractId, owner));
            ActiveFuturesRoutes.Add(futuresContractId);
            return added;
        }
    }

    public bool StopFuturesRoute(TickerStreamOwner owner, string futuresContractId)
    {
        lock (FuturesRouteSync)
        {
            var removed = ActiveStreamOwners.Remove((futuresContractId, owner));
            if (!ActiveStreamOwners.Any(item => item.ContractId == futuresContractId))
                ActiveFuturesRoutes.Remove(futuresContractId);
            return removed;
        }
    }

    public bool StartIndividualOptionRoute(TickerStreamOwner owner, string futuresOptionContractId)
    {
        lock (FuturesRouteSync)
        {
            if (!ActiveStreamOwners.Add((futuresOptionContractId, owner))) return false;
            if (ActiveStreamOwners.Count(item => item.ContractId == futuresOptionContractId) == 1)
                OptionRoutes.StartIndividual(futuresOptionContractId);
            return true;
        }
    }

    public bool StopIndividualOptionRoute(TickerStreamOwner owner, string futuresOptionContractId)
    {
        lock (FuturesRouteSync)
        {
            if (!ActiveStreamOwners.Remove((futuresOptionContractId, owner))) return false;
            if (!ActiveStreamOwners.Any(item => item.ContractId == futuresOptionContractId))
                OptionRoutes.StopIndividual(futuresOptionContractId);
            return true;
        }
    }

    public async Task<bool> StartOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds)
    {
        _ = await TreasuryCurve.GetLatestAsync(ValueDate);
        return OptionRoutes.StartChain(
            futuresContractId, maturityDate, optionContractIds);
    }

    public Task<bool> StopOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate) =>
        Task.FromResult(OptionRoutes.StopChain(futuresContractId, maturityDate));

    internal FakeFuturesLastPriceReader GetFuturesReader(string contractId)
    {
        lock (futuresReaders)
        {
            if (!futuresReaders.TryGetValue(contractId, out var reader))
            {
                reader = new FakeFuturesLastPriceReader(contractId, ValueDate);
                futuresReaders.Add(contractId, reader);
            }
            return reader;
        }
    }

    internal FakeFuturesOptionLastPriceReader GetOptionReader(string contractId)
    {
        lock (optionReaders)
        {
            if (!optionReaders.TryGetValue(contractId, out var reader))
            {
                reader = new FakeFuturesOptionLastPriceReader(contractId, ValueDate);
                optionReaders.Add(contractId, reader);
            }
            return reader;
        }
    }

    IFuturesLastPriceReader IDatabentoLastPriceReaderProvider.GetFuturesReader(
        string futuresContractId,
        DateOnly valueDate) => GetFuturesReader(futuresContractId);

    IFuturesOptionLastPriceReader IDatabentoLastPriceReaderProvider.GetFuturesOptionReader(
        string futuresOptionContractId,
        DateOnly valueDate) => GetOptionReader(futuresOptionContractId);

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        foreach (var reader in futuresReaders.Values)
        {
            reader.Invalidate();
        }
        foreach (var reader in optionReaders.Values)
        {
            reader.Invalidate();
        }
        lifecycleLog.Add($"Dispose:{ValueDate:yyyy-MM-dd}");
        return ValueTask.CompletedTask;
    }

    private static TickerPriceSnapshot ToTickerPrice(FuturesMarketPriceSnapshot price) => new(
        price.ContractId,
        price.InstrumentId,
        price.PublisherId,
        price.AssetTypeId,
        price.ValueDate,
        price.Quote is { } quote
            ? new TickerQuoteSnapshot(
                quote.BidPrice, quote.BidSize, quote.AskPrice, quote.AskSize,
                quote.BidCount, quote.AskCount, quote.SourceSequence,
                quote.EventTimestamp, quote.ReceiveTimestamp)
            : null,
        price.Trade is { } trade
            ? new TickerTradeSnapshot(
                trade.LastPrice, trade.LastSize, trade.SourceSequence,
                trade.EventTimestamp, trade.ReceiveTimestamp)
            : null);
}

internal sealed class FakeLifecycleStage(
    string name,
    List<string> lifecycleLog,
    (TaskCompletionSource Entered, TaskCompletionSource Release)? gate,
    bool failOnStart)
{
    private bool started;

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        lifecycleLog.Add($"Start:{name}");
        if (gate is { } startGate)
        {
            startGate.Entered.TrySetResult();
            await startGate.Release.Task.WaitAsync(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (failOnStart)
        {
            throw new InvalidOperationException($"Injected start failure at {name}.");
        }
        started = true;
    }

    internal Task StopAsync()
    {
        if (started)
        {
            lifecycleLog.Add($"Stop:{name}");
            started = false;
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakeMarketDataCatalog : IDatabentoMarketDataCatalog
{
    internal Dictionary<string, FuturesContractV2ReadModel> Futures { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, FuturesOptionContractReadModel> Options { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, string> OptionUnderlyings { get; } =
        new(StringComparer.Ordinal);
    internal int ProviderQueryCount;

    internal FuturesContractV2ReadModel? FindFuture(string contractId)
    {
        Interlocked.Increment(ref ProviderQueryCount);
        return Futures.GetValueOrDefault(contractId);
    }

    internal FuturesOptionContractReadModel? FindOption(string contractId)
    {
        Interlocked.Increment(ref ProviderQueryCount);
        return Options.GetValueOrDefault(contractId);
    }

    public FuturesContractV2ReadModel? FindFutures(string contractId) =>
        FindFuture(contractId);

    public FuturesOptionContractReadModel? FindFuturesOption(string contractId) =>
        FindOption(contractId);

    public string? FindOptionUnderlying(string futuresOptionContractId) =>
        OptionUnderlyings.GetValueOrDefault(futuresOptionContractId);

    public Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        Interlocked.Increment(ref ProviderQueryCount);
        return Task.FromResult(Options.Values
            .Where(option => OptionUnderlyings.GetValueOrDefault(option.ContractId)
                    == futuresContractId
                && option.ContractMonth == maturityDate)
            .OrderBy(option => option.StrikePrice)
            .ThenBy(option => option.OptionType, StringComparer.Ordinal)
            .ThenBy(option => option.ContractId, StringComparer.Ordinal)
            .ToArray());
    }
}

internal sealed class FakeTickAggregationStatus
{
    internal bool ServiceRunning { get; set; } = true;
    internal HashSet<string> ConfiguredTickers { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> RunningTickers { get; } = new(StringComparer.Ordinal);
    internal int StatusQueryCount { get; private set; }

    internal (bool ServiceRunning, bool TickerConfigured, bool TickerRunning) GetStatus(
        string futuresContractId)
    {
        StatusQueryCount++;
        return (
            ServiceRunning,
            ConfiguredTickers.Contains(futuresContractId),
            RunningTickers.Contains(futuresContractId));
    }
}

internal sealed class FakeTreasuryCurve : ITreasuryCurve
{
    internal int QueryCount { get; private set; }
    internal decimal RiskFreeRate { get; set; } = 0.04m;

    public Task<TreasuryCurveSnapshot?> GetLatestAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QueryCount++;
        TreasuryCurveSnapshot snapshot = new(
            asOfDate,
            [new TreasuryRatePoint(TreasuryTenor.OneMonth, RiskFreeRate * 100m)],
            new DateTimeOffset(asOfDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            "Gate4Fake");
        return Task.FromResult<TreasuryCurveSnapshot?>(snapshot);
    }

    public async Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestAsync(toInclusive, cancellationToken);
        return latest is null || latest.ValueDate < fromInclusive ? [] : [latest];
    }
}

internal sealed class FakeOptionRouteRegistry
{
    private readonly object sync = new();
    private readonly Dictionary<string, string> owners = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Underlying, DateOnly Maturity), string[]> chains = [];

    internal bool IsOwned(string optionContractId)
    {
        lock (sync) return owners.ContainsKey(optionContractId);
    }

    internal bool StartIndividual(string optionContractId)
    {
        lock (sync)
        {
            if (!owners.TryGetValue(optionContractId, out var owner))
            {
                owners.Add(optionContractId, "individual");
                return true;
            }
            if (owner == "individual")
            {
                return false;
            }
            throw new Contracts.MarketDataRouteConflictException(optionContractId, owner);
        }
    }

    internal bool StopIndividual(string optionContractId)
    {
        lock (sync)
        {
            return owners.TryGetValue(optionContractId, out var owner)
                   && owner == "individual"
                   && owners.Remove(optionContractId);
        }
    }

    internal bool StartChain(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds)
    {
        lock (sync)
        {
            var key = (futuresContractId, maturityDate);
            var normalized = optionContractIds
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (chains.TryGetValue(key, out var existing))
            {
                if (existing.SequenceEqual(normalized, StringComparer.Ordinal))
                {
                    return false;
                }
                throw new Contracts.OptionChainConflictException(futuresContractId, maturityDate);
            }

            foreach (var optionContractId in normalized)
            {
                if (owners.TryGetValue(optionContractId, out var owner))
                {
                    throw new Contracts.MarketDataRouteConflictException(optionContractId, owner);
                }
            }

            var chainOwner = $"chain:{futuresContractId}:{maturityDate:yyyy-MM-dd}";
            foreach (var optionContractId in normalized)
            {
                owners.Add(optionContractId, chainOwner);
            }
            chains.Add(key, normalized);
            return true;
        }
    }

    internal bool StopChain(string futuresContractId, DateOnly maturityDate)
    {
        lock (sync)
        {
            var key = (futuresContractId, maturityDate);
            if (!chains.Remove(key, out var options))
            {
                return false;
            }
            foreach (var option in options)
            {
                owners.Remove(option);
            }
            return true;
        }
    }

    internal void Clear()
    {
        lock (sync)
        {
            owners.Clear();
            chains.Clear();
        }
    }
}

internal abstract class FakeLastPriceReader
{
    private readonly object sync = new();
    private bool active = true;
    private LastTradeTickSnapshot? trade;
    private LastQuoteTickSnapshot? quote;
    private LastTradeTickWithGreeksSnapshot? tradeWithGreeks;
    private LastQuoteTickWithGreeksSnapshot? quoteWithGreeks;

    internal void SetTrade(LastTradeTickSnapshot snapshot)
    {
        lock (sync)
        {
            trade = snapshot;
            tradeWithGreeks = null;
        }
    }

    internal void SetQuote(LastQuoteTickSnapshot snapshot)
    {
        lock (sync)
        {
            quote = snapshot;
            quoteWithGreeks = null;
        }
    }

    internal void SetTradeWithGreeks(LastTradeTickWithGreeksSnapshot snapshot)
    {
        lock (sync)
        {
            trade = snapshot.Tick;
            tradeWithGreeks = snapshot;
        }
    }

    internal void SetQuoteWithGreeks(LastQuoteTickWithGreeksSnapshot snapshot)
    {
        lock (sync)
        {
            quote = snapshot.Tick;
            quoteWithGreeks = snapshot;
        }
    }

    internal bool TryReadTrade(out LastTradeTickSnapshot snapshot)
    {
        lock (sync)
        {
            if (active && trade is { } value)
            {
                snapshot = value;
                return true;
            }
            snapshot = default;
            return false;
        }
    }

    internal bool TryReadQuote(out LastQuoteTickSnapshot snapshot)
    {
        lock (sync)
        {
            if (active && quote is { } value)
            {
                snapshot = value;
                return true;
            }
            snapshot = default;
            return false;
        }
    }

    internal bool TryReadTradeWithGreeks(
        out LastTradeTickWithGreeksSnapshot snapshot)
    {
        lock (sync)
        {
            if (active && tradeWithGreeks is { } value)
            {
                snapshot = value;
                return true;
            }
            snapshot = default;
            return false;
        }
    }

    internal bool TryReadQuoteWithGreeks(
        out LastQuoteTickWithGreeksSnapshot snapshot)
    {
        lock (sync)
        {
            if (active && quoteWithGreeks is { } value)
            {
                snapshot = value;
                return true;
            }
            snapshot = default;
            return false;
        }
    }

    internal void Invalidate()
    {
        lock (sync)
        {
            active = false;
            trade = null;
            quote = null;
            tradeWithGreeks = null;
            quoteWithGreeks = null;
        }
    }
}

internal sealed class FakeFuturesLastPriceReader(
    string futuresContractId,
    DateOnly valueDate) : FakeLastPriceReader, IFuturesLastPriceReader
{
    public string FuturesContractId { get; } = futuresContractId;
    public DateOnly ValueDate { get; } = valueDate;
    public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot) => TryReadTrade(out snapshot);
    public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot) => TryReadQuote(out snapshot);
}

internal sealed class FakeFuturesOptionLastPriceReader(
    string futuresOptionContractId,
    DateOnly valueDate) : FakeLastPriceReader, IFuturesOptionLastPriceReader
{
    public string FuturesOptionContractId { get; } = futuresOptionContractId;
    public DateOnly ValueDate { get; } = valueDate;
    public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot) => TryReadTrade(out snapshot);
    public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot) => TryReadQuote(out snapshot);
    public bool TryGetLastTradeWithGreeks(
        out LastTradeTickWithGreeksSnapshot snapshot) =>
        TryReadTradeWithGreeks(out snapshot);
    public bool TryGetLastQuoteWithGreeks(
        out LastQuoteTickWithGreeksSnapshot snapshot) =>
        TryReadQuoteWithGreeks(out snapshot);
}
