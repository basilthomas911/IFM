using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

internal static class ApplicationPriceBenchmark
{
    internal static int Run(string[] args)
    {
        var operations = ReadOperations(args);
        var strict = args.Contains("--strict", StringComparer.OrdinalIgnoreCase);
        var valueDate = new DateOnly(2026, 8, 10);
        var now = new DateTimeOffset(2026, 8, 10, 14, 30, 0, TimeSpan.Zero);
        var epoch = new BenchmarkEpoch(valueDate, now);
        var api = new DatabentoMarketDataApi(
            new BenchmarkEpochFactory(epoch),
            new DatabentoMarketDataApiOptions
            {
                MaximumLastPriceAge = TimeSpan.FromSeconds(10)
            },
            new FixedTimeProvider(now));
        api.StartAsync(valueDate).GetAwaiter().GetResult();

        for (var index = 0; index < 100_000; index++)
            _ = api.GetFuturesPriceAsync("ESU6").GetAwaiter().GetResult();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        decimal checksum = 0m;
        for (var index = 0; index < operations; index++)
            checksum += api.GetFuturesPriceAsync("ESU6").GetAwaiter().GetResult();
        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        var rate = operations / stopwatch.Elapsed.TotalSeconds;

        api.StopAsync(valueDate).GetAwaiter().GetResult();
        api.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Console.WriteLine("Application market-data price facade benchmark");
        Console.WriteLine($"Operations: {operations:N0}");
        Console.WriteLine(
            $"Futures price calls: {rate:N0}/s, {allocated:N0} B allocated " +
            $"({(double)allocated / operations:N3} B/op)");
        Console.WriteLine($"Checksum: {checksum}");

        if (strict && (rate < 1_000_000 || (double)allocated / operations > 128d))
        {
            Console.Error.WriteLine("Strict application price facade qualification failed.");
            return 5;
        }
        return 0;
    }

    private static int ReadOperations(string[] args)
    {
        var argument = args.FirstOrDefault(static value =>
            value.StartsWith("--operations=", StringComparison.OrdinalIgnoreCase));
        return argument is null
            ? 1_000_000
            : int.Parse(argument.AsSpan(argument.IndexOf('=') + 1));
    }

    private sealed class BenchmarkEpochFactory(BenchmarkEpoch epoch)
        : IDatabentoMarketDataEpochFactory
    {
        public IDatabentoMarketDataEpoch Create(DateOnly valueDate) => epoch;
    }

    private sealed class BenchmarkEpoch : IDatabentoMarketDataEpoch
    {
        private readonly DatabentoLastPriceStore _store;
        private readonly BenchmarkCatalog _catalog;

        internal BenchmarkEpoch(DateOnly valueDate, DateTimeOffset now)
        {
            ValueDate = valueDate;
            _store = new DatabentoLastPriceStore(valueDate, 1);
            _store.RegisterContract("ESU6", AssetTypeId.Futures);
            _store.TryUpdateTrade(new(
                "ESU6", valueDate, 6500.25m, 1, 1,
                now.AddMilliseconds(-1), now));
            _catalog = new BenchmarkCatalog(valueDate);
        }

        public DateOnly ValueDate { get; }
        public IDatabentoMarketDataCatalog Catalog => _catalog;
        public IDatabentoLastPriceReaderProvider LastPrices => _store;
        public ITickerDataReaderFactory TickerReaders { get; } = new UnsupportedTickerReaders();
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync()
        {
            _store.Invalidate();
            return Task.CompletedTask;
        }
        public TickAggregationContractStatus GetAggregationStatus(string contractId) =>
            new(contractId, AssetTypeId.Futures, true, true, true);
        public DatabentoMarketDataEpochHealth GetHealth() => new(
            ValueDate, true, true, 1, 1, true, 0, 1, 0);
        public bool StartFuturesRoute(string futuresContractId) => true;
        public bool StopFuturesRoute(string futuresContractId) => true;
        public bool StartIndividualOptionRoute(string futuresOptionContractId) => true;
        public bool StopIndividualOptionRoute(string futuresOptionContractId) => true;
        public Task<bool> StartOptionChainAsync(
            string futuresContractId,
            DateOnly maturityDate,
            string[] optionContractIds) => Task.FromResult(true);
        public Task<bool> StopOptionChainAsync(
            string futuresContractId,
            DateOnly maturityDate) => Task.FromResult(true);
        public ValueTask DisposeAsync()
        {
            _store.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UnsupportedTickerReaders : ITickerDataReaderFactory
    {
        public ValueTask<ITickerDataReader> CreateAsync(
            TickerReaderOwner owner,
            string contractId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<ITickerDataReader>(
                new NotSupportedException("Ticker-reader creation is outside this price benchmark."));
    }

    private sealed class BenchmarkCatalog : IDatabentoMarketDataCatalog
    {
        private readonly FuturesContractV2ReadModel _future;

        internal BenchmarkCatalog(DateOnly maturity)
        {
            _future = new FuturesContractV2ReadModel(
                "ESU6", "ES", "ES", "ESU6", "FUT", "USD", "CME", "50",
                maturity, true);
        }
        public FuturesContractV2ReadModel? FindFutures(string contractId) =>
            contractId == "ESU6" ? _future : null;
        public FuturesOptionContractReadModel? FindFuturesOption(string contractId) => null;
        public string? FindOptionUnderlying(string futuresOptionContractId) => null;
        public Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(
            string futuresContractId,
            DateOnly maturityDate) => Task.FromResult(Array.Empty<FuturesOptionContractReadModel>());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
