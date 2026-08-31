using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class DatabentoOptionChainSessionManagerTests
{
    private static readonly DateOnly ValueDate = new(2026, 8, 10);
    private static readonly DateOnly Maturity = new(2026, 9, 18);
    private static readonly InstrumentKey Instrument = new(7, 43);

    [Fact]
    public async Task Session_demultiplexes_transient_quote_and_trade_and_drains_on_stop()
    {
        var aggregation = new FakeAggregation();
        var feed = new FakeChainFeed(
            Quote(1, 10_000_000_000, 12_000_000_000),
            Trade(2, 11_000_000_000));
        var factory = new FakeFactory(feed);
        using var lastPrices = new DatabentoLastPriceStore(ValueDate, 1);
        var publisher = new CapturingChainPublisher(2);
        var state = new OptionChainStateStore();
        await using var manager = new DatabentoOptionChainSessionManager(
            factory,
            DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            aggregation,
            lastPrices,
            new FakeEnricher(),
            publisher,
            state,
            pollTimeout: TimeSpan.FromMilliseconds(5));
        var request = Request();

        Assert.True(await manager.StartAsync(request));
        Assert.False(await manager.StartAsync(request));
        await publisher.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(publisher.Quotes);
        Assert.Single(publisher.Trades);
        var key = new OptionChainSessionKey("ES-202609", Maturity);
        Assert.True(state.TryGet(key, "ES20260918C6500", out var observed));
        Assert.Equal(1, observed.Quote!.Value.Tick.SourceSequence);
        Assert.Equal(2, observed.Trade!.Value.Tick.SourceSequence);
        var reader = lastPrices.GetFuturesOptionReader("ES20260918C6500", ValueDate);
        Assert.True(reader.TryGetLastQuoteWithGreeks(out var quote));
        Assert.Equal(1, quote.Greeks.OptionPriceSourceSequence);
        Assert.True(reader.TryGetLastTradeWithGreeks(out var trade));
        Assert.Equal(2, trade.Tick.SourceSequence);

        Assert.True(await manager.StopAsync("ES-202609", Maturity));
        Assert.False(await manager.StopAsync("ES-202609", Maturity));
        Assert.Empty(state.GetSession(key));
        Assert.Equal(1, feed.StopCount);
        Assert.True(feed.Disposed);
    }

    [Fact]
    public async Task Underlying_dependency_is_checked_before_feed_allocation()
    {
        var aggregation = new FakeAggregation { ServiceRunning = false };
        var factory = new FakeFactory(new FakeChainFeed());
        using var lastPrices = new DatabentoLastPriceStore(ValueDate, 1);
        await using var manager = new DatabentoOptionChainSessionManager(
            factory,
            DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            aggregation,
            lastPrices,
            new FakeEnricher(),
            new CapturingChainPublisher(1),
            new OptionChainStateStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync(Request()));
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task Duplicate_option_record_does_not_update_state_or_publish()
    {
        var feed = new FakeChainFeed(
            Quote(1, 10_000_000_000, 12_000_000_000),
            Quote(1, 11_000_000_000, 13_000_000_000));
        using var lastPrices = new DatabentoLastPriceStore(ValueDate, 1);
        var publisher = new CapturingChainPublisher(1);
        var state = new OptionChainStateStore();
        await using var manager = new DatabentoOptionChainSessionManager(
            new FakeFactory(feed),
            DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            new FakeAggregation(),
            lastPrices,
            new FakeEnricher(),
            publisher,
            state,
            pollTimeout: TimeSpan.FromMilliseconds(5));

        Assert.True(await manager.StartAsync(Request()));
        await publisher.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await manager.StopAsync("ES-202609", Maturity);

        Assert.Single(publisher.Quotes);
        Assert.Equal(10m, publisher.Quotes[0].Tick.BidPrice);
    }

    [Fact]
    public async Task Dependency_loss_stops_feed_and_removes_transient_state()
    {
        var aggregation = new FakeAggregation();
        var feed = new FakeChainFeed();
        var state = new OptionChainStateStore();
        using var lastPrices = new DatabentoLastPriceStore(ValueDate, 1);
        await using var manager = new DatabentoOptionChainSessionManager(
            new FakeFactory(feed),
            DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            aggregation,
            lastPrices,
            new FakeEnricher(),
            new CapturingChainPublisher(1),
            state,
            pollTimeout: TimeSpan.FromMilliseconds(5));
        await manager.StartAsync(Request());

        aggregation.ServiceRunning = false;

        Assert.True(SpinWait.SpinUntil(
            () => manager.ActiveSessionCount == 0,
            TimeSpan.FromSeconds(2)));
        Assert.Equal(1, feed.StopCount);
        Assert.True(feed.Disposed);
        Assert.Empty(state.GetSession(new OptionChainSessionKey("ES-202609", Maturity)));
    }

    private static DatabentoOptionChainSessionRequest Request()
    {
        var definition = new OptionContractDefinition
        {
            Dataset = "GLBX.MDP3",
            RawSymbol = "ESU6 C6500",
            Ticker = "ES",
            Underlying = "ESU6",
            Instrument = Instrument,
            Right = OptionRightSelection.Call,
            StrikePrice = 6500m,
            MaturityDate = Maturity,
            ContractMultiplier = 50
        };
        return new DatabentoOptionChainSessionRequest
        {
            FuturesContractId = "ES-202609",
            ValueDate = ValueDate,
            Subscription = new OptionChainSubscription
            {
                Underlying = "ESU6",
                MaturityDate = Maturity,
                Strikes = [6500m],
                Rights = OptionRightSelection.Call,
                ResolvedContracts = [definition],
                DataKinds = MarketDataKinds.Quote | MarketDataKinds.Trade
            },
            Routes =
            [
                new DatabentoOptionChainRoute
                {
                    FuturesOptionContractId = "ES20260918C6500",
                    Definition = definition
                }
            ]
        };
    }

    private static MarketRecord64 Quote(uint sequence, long bid, long ask) => new(
        new QuoteRecord64(
            new MarketRecordHeader32(
                Instrument.InstrumentId, Instrument.PublisherId,
                MarketRecordKind.Quote, 0, sequence, sequence, sequence),
            bid, ask, 10, 11, 1, 1));

    private static MarketRecord64 Trade(uint sequence, long price) => new(
        new TradeRecord64(
            new MarketRecordHeader32(
                Instrument.InstrumentId, Instrument.PublisherId,
                MarketRecordKind.Trade, 0, sequence, sequence, sequence),
            price, 12, 1, 2, 0));

    private sealed class FakeAggregation : ITickAggregationService
    {
        internal bool ServiceRunning { get; set; } = true;
        public bool IsRunning => ServiceRunning;
        public TickAggregationContractStatus GetContractStatus(string contractId) =>
            new(contractId, AssetTypeId.Futures, ServiceRunning, true, ServiceRunning);
        public TickAggregationTickerStatus GetTickerStatus(string futuresContractId) =>
            new(futuresContractId, ServiceRunning, true, ServiceRunning);
        public bool TryGetLastTickPrice(
            string contractId,
            out FuturesMarketPriceSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
        public bool TryGetLastOptionTickPrice(
            string contractId,
            out OptionTickerPriceSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
        public bool IsTickDataStreamActive(string contractId) => false;
        public bool StartTickDataStream(TickerStreamOwner owner, string contractId) => true;
        public bool StopTickDataStream(TickerStreamOwner owner, string contractId) => true;
        public ValueTask StartAsync() => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFactory(FakeChainFeed feed) : IDatabentoFeedFactory
    {
        internal int CreateCount { get; private set; }
        public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options)
        {
            CreateCount++;
            return feed;
        }
        public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
        public IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
        public IDatabentoLatestPriceClient CreateLatestPriceClient(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
    }

    private sealed class FakeChainFeed(params MarketRecord64[] records)
        : IDatabentoOptionChainFeed
    {
        private readonly BoundedBatchChannel _channel = new(4, 64);
        internal int StopCount { get; private set; }
        internal bool Disposed { get; private set; }
        public ISynchronousBatchReader<MarketDataBatch64> Reader => _channel;
        public void Subscribe(OptionChainSubscription subscription, TimeSpan timeout) { }
        public void Start(TimeSpan timeout)
        {
            if (records.Length == 0) return;
            var batch = _channel.RentBatch(static () => false);
            foreach (var record in records) batch.Add(record);
            Assert.True(_channel.Publish(batch, static () => false));
        }
        public void Stop(TimeSpan timeout)
        {
            StopCount++;
            _channel.Complete();
        }
        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose()
        {
            Disposed = true;
            _channel.Complete();
        }
    }

    private sealed class CapturingChainPublisher(int expected)
        : IOptionChainTransientEventPublisher
    {
        public List<FuturesOptionChainQuoteChangedServiceEvent> Quotes { get; } = [];
        public List<FuturesOptionChainTradeChangedServiceEvent> Trades { get; } = [];
        internal TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent @event)
        {
            Quotes.Add(@event);
            CompleteIfReady();
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent @event)
        {
            Trades.Add(@event);
            CompleteIfReady();
            return ValueTask.CompletedTask;
        }
        private void CompleteIfReady()
        {
            if (Quotes.Count + Trades.Count >= expected) Completed.TrySetResult();
        }
    }

    private sealed class FakeEnricher : IOptionChainGreeksEnricher
    {
        public OptionGreeksSnapshot EnrichQuote(
            DatabentoOptionChainRoute route,
            LastQuoteTickSnapshot tick) => Greeks(tick.SourceSequence);
        public OptionGreeksSnapshot EnrichTrade(
            DatabentoOptionChainRoute route,
            LastTradeTickSnapshot tick) => Greeks(1);

        private static OptionGreeksSnapshot Greeks(long optionSequence) => new(
            false, false, OptionGreeksFailureReason.InvalidRiskFreeRate,
            OptionGreeksPriceSource.QuoteMidpoint, "ES-202609", null, null,
            null, null, null, null, null, null, null, null, null, 0,
            0, optionSequence, default, default, DateTimeOffset.UtcNow);
    }
}
