using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoProductionEpochTests
{
    [Fact]
    public async Task Real_epoch_composes_queries_feed_aggregation_routes_and_shutdown()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var maturity = new DateOnly(2026, 9, 18);
        var future = Detail(
            "ESU6", "ES", new InstrumentKey(7, 42), ContractKind.Future,
            maturity, null, "ESU6");
        var option = Detail(
            "ESU6 C6500", "ES", new InstrumentKey(7, 43), ContractKind.CallOption,
            maturity, 6_500_123_456_789, "ESU6");
        var provider = new FakeFeedFactory([future, option]);
        provider.CatalogQueries.FailuresRemaining = 1;
        var runtimeOptions = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            Contracts =
            [
                new DatabentoContractRegistration
                {
                    DomainContractId = "ES-202609",
                    ProviderContractName = "ESU6",
                    AssetTypeId = AssetTypeId.Futures
                },
                new DatabentoContractRegistration
                {
                    DomainContractId = "ES20260918C6500",
                    ProviderContractName = "ESU6 C6500",
                    AssetTypeId = AssetTypeId.FuturesOption
                }
            ],
            QueryConcurrency = 1,
            QueryQueueCapacity = 4,
            LastPriceCapacity = 2,
            CatalogQueryRetryDelay = TimeSpan.Zero
        };
        var publisher = new NoOpPublisher();
        var epochFactory = new DatabentoMarketDataEpochFactory(
            provider, publisher, runtimeOptions);
        await using var api = new DatabentoMarketDataApi(
            epochFactory,
            new DatabentoMarketDataApiOptions());

        await api.StartAsync(valueDate);

        var mappedFuture = await api.GetFuturesContractAsync("ES-202609");
        Assert.NotNull(mappedFuture);
        Assert.Equal("ESU6", mappedFuture.LocalSymbol);
        var mappedOption = await api.GetFuturesOptionContractAsync("ES20260918C6500");
        Assert.NotNull(mappedOption);
        Assert.Equal(6500.123456789d, mappedOption.StrikePrice);
        Assert.Same(
            api.GetFuturesLastPriceReader("ES-202609"),
            api.GetFuturesLastPriceReader("ES-202609"));
        var health = api.GetHealth();
        Assert.True(health.Running);
        Assert.True(api.IsDatabentoFeedUp());
        Assert.Equal(2, health.Epoch!.Value.ConfiguredContracts);
        Assert.Equal(2, health.Epoch.Value.LastPriceSlots);
        var datasetHealth = Assert.Single(health.Epoch.Value.DatasetFeedStatuses!);
        Assert.Equal("GLBX.MDP3", datasetHealth.Dataset);
        Assert.Equal(FeedState.Running, datasetHealth.Health.State);
        Assert.Equal(DatabentoFeedStatus.Ok, datasetHealth.Health.TerminalStatus);
        Assert.True(datasetHealth.Health.TransportReady);
        Assert.True(await api.StartStreamingFuturesTickDataAsync("ES-202609"));
        Assert.False(await api.StartStreamingFuturesTickDataAsync("ES-202609"));
        Assert.True(await api.StartStreamingFuturesOptionTickDataAsync("ES20260918C6500"));
        Assert.False(await api.StartStreamingFuturesOptionTickDataAsync("ES20260918C6500"));
        await Assert.ThrowsAsync<MarketDataPricingInputUnavailableException>(() =>
            api.StartStreamingFuturesOptionChainDataAsync(
                "ES-202609", maturity, ["ES20260918C6500"]));

        await api.StopAsync(valueDate);

        Assert.False(api.IsDatabentoFeedUp());
        Assert.Null(api.ActiveValueDate);
        Assert.Equal(1, provider.Feed.StartCount);
        Assert.Equal(1, provider.Feed.StopCount);
        Assert.Equal(TimeSpan.FromSeconds(5), provider.Feed.StopTimeout);
        Assert.True(provider.Feed.Disposed);
        Assert.Equal(2, provider.CatalogQueries.Attempts);
    }

    [Fact]
    public async Task One_logical_epoch_partitions_contracts_across_provider_datasets()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var es = Detail(
            "ESU6", "ES", new InstrumentKey(7, 42), ContractKind.Future,
            new DateOnly(2026, 9, 18), null, "ESU6");
        var vx = Detail(
            "VXU6", "VX", new InstrumentKey(8, 84), ContractKind.Future,
            new DateOnly(2026, 9, 16), null, "VXU6",
            "XCBF.PITCH", "CFE");
        var vxBack = Detail(
            "VXV6", "VX", new InstrumentKey(8, 85), ContractKind.Future,
            new DateOnly(2026, 10, 21), null, "VXV6",
            "XCBF.PITCH", "CFE");
        using var concurrentStopBarrier = new CountdownEvent(2);
        var provider = new FakeFeedFactory([es, vx, vxBack])
        {
            StopBarrier = concurrentStopBarrier
        };
        var configuredOptions = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            Contracts = [],
            QueryConcurrency = 1,
            QueryQueueCapacity = 4,
            LastPriceCapacity = 3
        };
        var registry = new DatabentoContractRegistrationRegistry([], configuredOptions);
        registry.ReplaceFuturesRolloverSet("ES", [
            new FuturesContractV3ReadModel(
                "ES20260918", "ES future", "ES", "ESU6", "FUT", "USD",
                "CME", "50", new DateOnly(2026, 9, 18), true, true)]);
        registry.ReplaceFuturesRolloverSet("VX", [
            new FuturesContractV3ReadModel(
                "VX20260916", "VX future", "VX", "VXU6", "FUT", "USD",
                "CFE", "1000", new DateOnly(2026, 9, 16), true, true),
            new FuturesContractV3ReadModel(
                "VX20261021", "VX future", "VX", "VXV6", "FUT", "USD",
                "CFE", "1000", new DateOnly(2026, 10, 21), false, true)]);
        var runtimeOptions = configuredOptions with { Contracts = registry };
        var publisher = new NoOpPublisher();
        var epochFactory = new DatabentoMarketDataEpochFactory(
            provider, publisher, runtimeOptions);
        await using var api = new DatabentoMarketDataApi(
            epochFactory, new DatabentoMarketDataApiOptions());

        await api.StartAsync(valueDate);

        Assert.NotNull(await api.GetFuturesContractAsync("ES20260918"));
        Assert.NotNull(await api.GetFuturesContractAsync("VX20260916"));
        Assert.Equal(["GLBX.MDP3", "XCBF.PITCH"],
            provider.Feeds.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["XCBF.PITCH", "GLBX.MDP3"], provider.FeedStartOrder);
        Assert.Equal(
            MarketDataKinds.Quote | MarketDataKinds.Trade
                | MarketDataKinds.Statistics | MarketDataKinds.SessionVolume,
            provider.Feeds["GLBX.MDP3"].Subscriptions.Single().DataKinds);
        Assert.Equal(2, provider.Feeds["XCBF.PITCH"].Subscriptions.Count);
        Assert.All(provider.Feeds["XCBF.PITCH"].Subscriptions, subscription => Assert.Equal(
            MarketDataKinds.Quote | MarketDataKinds.Trade
                | MarketDataKinds.Statistics | MarketDataKinds.SessionVolume,
            subscription.DataKinds));
        var expectedReplayStart = checked((ulong)(
            FuturesTradingValueDate.GetSessionStartUtc(valueDate).UtcTicks
            - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL);
        Assert.All(provider.FeedOptions.Values, options => Assert.Equal(
            expectedReplayStart,
            options.StatisticsReplayStartTimestampNanoseconds));
        Assert.All(provider.FeedOptions.Values, options => Assert.Equal(
            expectedReplayStart,
            options.TradeReplayStartTimestampNanoseconds));
        Assert.True(await api.StartStreamingFuturesTickDataAsync("ES20260918"));
        Assert.True(await api.StartStreamingFuturesTickDataAsync("VX20260916"));
        Assert.True(api.IsDatabentoFeedUp());

        provider.Feeds["GLBX.MDP3"].HealthState = FeedState.Faulted;
        Assert.False(api.IsDatabentoFeedUp());
        provider.Feeds["GLBX.MDP3"].HealthState = FeedState.Running;
        provider.Feeds["GLBX.MDP3"].TerminalStatus = DatabentoFeedStatus.ConnectionHung;
        Assert.False(api.IsDatabentoFeedUp());
        provider.Feeds["GLBX.MDP3"].TerminalStatus = DatabentoFeedStatus.Ok;
        Assert.True(api.IsDatabentoFeedUp());

        provider.Feeds["XCBF.PITCH"].ThrowOnHealth = true;
        Assert.False(api.IsDatabentoFeedUp());
        provider.Feeds["XCBF.PITCH"].ThrowOnHealth = false;
        provider.Feeds["XCBF.PITCH"].HealthDelay = TimeSpan.FromMilliseconds(20);
        Assert.False(api.IsDatabentoFeedUp(TimeSpan.FromMilliseconds(1)));
        provider.Feeds["XCBF.PITCH"].HealthDelay = TimeSpan.Zero;
        Assert.False(api.IsDatabentoFeedUp(TimeSpan.Zero));
        Assert.False(api.IsDatabentoFeedUp(TimeSpan.FromTicks(-1)));
        Assert.True(api.IsDatabentoFeedUp(TimeSpan.FromSeconds(1)));

        await api.StopAsync(valueDate);

        Assert.True(concurrentStopBarrier.IsSet);
        Assert.All(provider.Feeds.Values, feed => Assert.Equal(1, feed.StartCount));
        Assert.All(provider.Feeds.Values, feed => Assert.Equal(1, feed.StopCount));
        Assert.All(provider.Feeds.Values, feed => Assert.True(feed.Disposed));
        Assert.Equal(1, publisher.StartCount);
        Assert.Equal(1, publisher.StopCount);
    }

    private static ContractDetail Detail(
        string rawSymbol,
        string ticker,
        InstrumentKey instrument,
        ContractKind kind,
        DateOnly maturity,
        long? strike,
        string underlying,
        string dataset = "GLBX.MDP3",
        string exchange = "CME") => new()
    {
        Dataset = dataset,
        RawSymbol = rawSymbol,
        Ticker = ticker,
        Underlying = underlying,
        Instrument = instrument,
        ContractKind = kind,
        StrikePrice = strike,
        MaturityDate = maturity,
        ContractMultiplier = 50,
        Currency = "USD",
        SettlementCurrency = "USD",
        Exchange = exchange,
        SecurityType = kind == ContractKind.Future ? "FUT" : "FOP",
        Cfi = string.Empty,
        UnitOfMeasure = "USD"
    };

    private sealed class FakeFeedFactory(IReadOnlyList<ContractDetail> details)
        : IDatabentoFeedFactory
    {
        internal CountdownEvent? CatalogQueryBarrier { get; init; }
        internal CountdownEvent? StopBarrier { get; init; }
        internal Dictionary<string, FakeTickerFeed> Feeds { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, DatabentoFeedOptions> FeedOptions { get; } =
            new(StringComparer.Ordinal);
        internal List<string> FeedStartOrder { get; } = [];
        internal CatalogQueryState CatalogQueries { get; } = new();
        internal FakeTickerFeed Feed => Feeds.Values.Single();
        public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options)
        {
            var feed = new FakeTickerFeed(
                details.Where(detail => detail.Dataset == options.Dataset).ToArray(),
                options.DataSource,
                StopBarrier);
            Feeds.Add(options.Dataset, feed);
            FeedOptions.Add(options.Dataset, options);
            FeedStartOrder.Add(options.Dataset);
            return feed;
        }
        public IDatabentoMarketDataQueries CreateMarketDataQueries(
            DatabentoFeedOptions options) => new FakeQueries(
                details.Where(detail => detail.Dataset == options.Dataset)
                    .ToDictionary(item => item.RawSymbol, StringComparer.Ordinal),
                CatalogQueryBarrier,
                CatalogQueries);
        public IDatabentoOptionChainFeed CreateOptionChainFeed(
            DatabentoFeedOptions options) => throw new NotSupportedException();
        public IDatabentoLatestPriceClient CreateLatestPriceClient(
            DatabentoFeedOptions options) => throw new NotSupportedException();
    }

    private sealed class FakeQueries(
        Dictionary<string, ContractDetail> details,
        CountdownEvent? catalogQueryBarrier,
        CatalogQueryState catalogQueries)
        : IDatabentoMarketDataQueries
    {
        public OptionChainDefinitions GetChainDefinitions(
            OptionChainDefinitionRequest request,
            TimeSpan? timeout = null) => new()
        {
            Dataset = "GLBX.MDP3",
            Underlying = request.Underlying,
            MaturityDate = request.MaturityDate,
            UniversePolicy = request.UniversePolicy,
            Rights = request.Rights,
            Contracts = []
        };

        public uint ContractIdToInstrumentId(string contractId, TimeSpan? timeout = null) =>
            throw new InvalidOperationException(
                "Epoch catalog startup must use its batch definition snapshot.");
        public string InstrumentIdToContractId(uint instrumentId, TimeSpan? timeout = null) =>
            throw new InvalidOperationException(
                "Epoch catalog startup must use its batch definition snapshot.");
        public ContractDetail? GetContractDetail(string contractName, TimeSpan? timeout = null) =>
            details.GetValueOrDefault(contractName);
        public IReadOnlyList<ContractDetail> GetContractDetails(
            string ticker,
            TimeSpan? timeout = null) =>
            details.Values.Where(item => item.Ticker == ticker).ToArray();
        public IReadOnlyList<ContractDetail?> GetContractDetails(
            string[] contractNames,
            TimeSpan? timeout = null) =>
            throw new InvalidOperationException(
                "Epoch catalog startup must use status-based definition queries.");

        public DatabentoContractDetailsQueryResult TryGetContractDetails(
            string[] contractNames,
            TimeSpan? timeout = null)
        {
            catalogQueries.Attempts++;
            if (catalogQueries.FailuresRemaining > 0)
            {
                catalogQueries.FailuresRemaining--;
                return DatabentoContractDetailsQueryResult.Failure(
                    DatabentoFeedStatus.DatabentoError,
                    "Transient catalog read failure.");
            }
            if (catalogQueryBarrier is not null)
            {
                catalogQueryBarrier.Signal();
                if (!catalogQueryBarrier.Wait(TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("Dataset catalogs were not queried concurrently.");
            }
            return DatabentoContractDetailsQueryResult.Success(
                contractNames.Select(name => details.GetValueOrDefault(name)).ToArray());
        }
    }

    internal sealed class CatalogQueryState
    {
        internal int Attempts { get; set; }
        internal int FailuresRemaining { get; set; }
    }

    private sealed class FakeTickerFeed(
        IReadOnlyList<ContractDetail> details,
        FeedDataSourceMode dataSource,
        CountdownEvent? stopBarrier)
        : IDatabentoTickerFeed
    {
        private readonly BlockingReader _reader = new();
        private readonly Dictionary<string, ContractDetail> _details =
            details.ToDictionary(item => item.RawSymbol, StringComparer.Ordinal);
        private TickerSubscription[] _subscriptions = [];

        internal int StartCount { get; private set; }
        internal int StopCount { get; private set; }
        internal TimeSpan? StopTimeout { get; private set; }
        internal bool Disposed { get; private set; }
        internal FeedState HealthState { get; set; } = FeedState.Created;
        internal DatabentoFeedStatus TerminalStatus { get; set; } = DatabentoFeedStatus.Ok;
        internal bool ThrowOnHealth { get; set; }
        internal TimeSpan HealthDelay { get; set; }
        internal IReadOnlyList<TickerSubscription> Subscriptions => _subscriptions;

        public void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout) =>
            _subscriptions = subscriptions.ToArray();
        public void Start(TimeSpan timeout, Action<TimeSpan> startConsumer)
        {
            StartCount++;
            startConsumer(timeout);
            HealthState = FeedState.Running;
        }
        public void Stop(TimeSpan timeout)
        {
            StopCount++;
            StopTimeout = timeout;
            if (stopBarrier is not null)
            {
                stopBarrier.Signal();
                if (!stopBarrier.Wait(TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("Dataset feeds were not stopped concurrently.");
            }
            HealthState = FeedState.Stopped;
            _reader.Complete();
        }
        public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument) =>
            throw new NotSupportedException();
        public IMultiplexedTickerBatchReader GetMultiplexedReader() => _reader;
        public IReadOnlyList<TickerInstrumentRegistration> GetInstruments() =>
            _subscriptions.Select((subscription, index) =>
            {
                var detail = _details[subscription.Symbol];
                return new TickerInstrumentRegistration(
                    subscription.Symbol,
                    detail.RawSymbol,
                    dataSource == FeedDataSourceMode.Synthetic
                        ? new InstrumentKey(1, checked((uint)index + 1))
                        : detail.Instrument);
            }).ToArray();
        public FeedHealthSnapshot GetHealth()
        {
            if (HealthDelay > TimeSpan.Zero)
                Thread.Sleep(HealthDelay);
            if (ThrowOnHealth)
                throw new InvalidOperationException("Injected feed-health failure.");
            var transportReady = HealthState == FeedState.Running
                && TerminalStatus == DatabentoFeedStatus.Ok;
            return new FeedHealthSnapshot(
                HealthState,
                TerminalStatus,
                1024,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                null)
            {
                TransportReady = transportReady,
                TradingReady = transportReady,
                InstrumentCount = _subscriptions.Length,
                BaselineReadyInstrumentCount = transportReady ? _subscriptions.Length : 0
            };
        }
        public void Dispose()
        {
            Disposed = true;
            HealthState = FeedState.Stopped;
            _reader.Complete();
        }
    }

    private sealed class BlockingReader : IMultiplexedTickerBatchReader
    {
        private readonly ManualResetEventSlim _completed = new(false);
        public bool IsCompleted => _completed.IsSet;
        public bool TryRead(out InstrumentBatch64 batch)
        {
            batch = default;
            return false;
        }
        public bool TryRead(TimeSpan timeout, out InstrumentBatch64 batch)
        {
            _completed.Wait(timeout);
            batch = default;
            return false;
        }
        public InstrumentBatch64 Read(TimeSpan timeout)
        {
            if (_completed.Wait(timeout)) throw new EndOfStreamException();
            throw new TimeoutException();
        }
        internal void Complete() => _completed.Set();
        public void Dispose() => _completed.Set();
    }

    private sealed class NoOpPublisher : ITickAggregationEventPublisher
    {
        public bool IsRunning { get; private set; }
        internal int StartCount { get; private set; }
        internal int StopCount { get; private set; }
        public ValueTask StartAsync()
        {
            StartCount++;
            IsRunning = true;
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event) =>
            ValueTask.CompletedTask;
        public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event) =>
            ValueTask.CompletedTask;
        public ValueTask PublishAsync(
            FuturesTickQuoteDataChangedEvent @event,
            ITickQuoteBufferLease lease)
        {
            lease.Dispose();
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync()
        {
            StopCount++;
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => StopAsync();
    }
}
