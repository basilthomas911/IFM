using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
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
            LastPriceCapacity = 2
        };
        var epochFactory = new DatabentoMarketDataEpochFactory(
            provider, new NoOpPublisher(), runtimeOptions);
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
        Assert.Equal(2, health.Epoch!.Value.ConfiguredContracts);
        Assert.Equal(2, health.Epoch.Value.LastPriceSlots);
        Assert.True(await api.StartStreamingFuturesTickDataAsync("ES-202609"));
        Assert.False(await api.StartStreamingFuturesTickDataAsync("ES-202609"));
        Assert.True(await api.StartStreamingFuturesOptionTickDataAsync("ES20260918C6500"));
        Assert.False(await api.StartStreamingFuturesOptionTickDataAsync("ES20260918C6500"));
        await Assert.ThrowsAsync<MarketDataPricingInputUnavailableException>(() =>
            api.StartStreamingFuturesOptionChainDataAsync(
                "ES-202609", maturity, ["ES20260918C6500"]));

        await api.StopAsync(valueDate);

        Assert.Null(api.ActiveValueDate);
        Assert.Equal(1, provider.Feed.StartCount);
        Assert.Equal(1, provider.Feed.StopCount);
        Assert.True(provider.Feed.Disposed);
    }

    private static ContractDetail Detail(
        string rawSymbol,
        string ticker,
        InstrumentKey instrument,
        ContractKind kind,
        DateOnly maturity,
        long? strike,
        string underlying) => new()
    {
        Dataset = "GLBX.MDP3",
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
        Exchange = "CME",
        SecurityType = kind == ContractKind.Future ? "FUT" : "FOP",
        Cfi = string.Empty,
        UnitOfMeasure = "USD"
    };

    private sealed class FakeFeedFactory(IReadOnlyList<ContractDetail> details)
        : IDatabentoFeedFactory
    {
        private readonly Dictionary<string, ContractDetail> _details =
            details.ToDictionary(item => item.RawSymbol, StringComparer.Ordinal);

        internal FakeTickerFeed Feed { get; } = new(details);
        public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) => Feed;
        public IDatabentoMarketDataQueries CreateMarketDataQueries(
            DatabentoFeedOptions options) => new FakeQueries(_details);
        public IDatabentoOptionChainFeed CreateOptionChainFeed(
            DatabentoFeedOptions options) => throw new NotSupportedException();
        public IDatabentoLatestPriceClient CreateLatestPriceClient(
            DatabentoFeedOptions options) => throw new NotSupportedException();
    }

    private sealed class FakeQueries(Dictionary<string, ContractDetail> details)
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
            details[contractId].Instrument.InstrumentId;
        public string InstrumentIdToContractId(uint instrumentId, TimeSpan? timeout = null) =>
            details.Values.Single(item => item.Instrument.InstrumentId == instrumentId).RawSymbol;
        public ContractDetail? GetContractDetail(string contractName, TimeSpan? timeout = null) =>
            details.GetValueOrDefault(contractName);
        public IReadOnlyList<ContractDetail> GetContractDetails(
            string ticker,
            TimeSpan? timeout = null) =>
            details.Values.Where(item => item.Ticker == ticker).ToArray();
        public IReadOnlyList<ContractDetail?> GetContractDetails(
            string[] contractNames,
            TimeSpan? timeout = null) =>
            contractNames.Select(name => details.GetValueOrDefault(name)).ToArray();
    }

    private sealed class FakeTickerFeed(IReadOnlyList<ContractDetail> details)
        : IDatabentoTickerFeed
    {
        private readonly BlockingReader _reader = new();
        private readonly Dictionary<string, ContractDetail> _details =
            details.ToDictionary(item => item.RawSymbol, StringComparer.Ordinal);
        private TickerSubscription[] _subscriptions = [];

        internal int StartCount { get; private set; }
        internal int StopCount { get; private set; }
        internal bool Disposed { get; private set; }

        public void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout) =>
            _subscriptions = subscriptions.ToArray();
        public void Start(TimeSpan timeout) => StartCount++;
        public void Stop(TimeSpan timeout)
        {
            StopCount++;
            _reader.Complete();
        }
        public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument) =>
            throw new NotSupportedException();
        public IMultiplexedTickerBatchReader GetMultiplexedReader() => _reader;
        public IReadOnlyList<TickerInstrumentRegistration> GetInstruments() =>
            _subscriptions.Select(subscription =>
            {
                var detail = _details[subscription.Symbol];
                return new TickerInstrumentRegistration(
                    subscription.Symbol, detail.RawSymbol, detail.Instrument);
            }).ToArray();
        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose()
        {
            Disposed = true;
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
        public ValueTask StartAsync()
        {
            IsRunning = true;
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event) =>
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
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => StopAsync();
    }
}
