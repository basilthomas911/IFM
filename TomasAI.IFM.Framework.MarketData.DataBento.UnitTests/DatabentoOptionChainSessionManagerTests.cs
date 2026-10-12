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
            Quote(1, 10_000_000_000, 12_000_000_000, publisherId: 9),
            Trade(2, 11_000_000_000, publisherId: 11));
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

        var beforeReceipt = DateTimeOffset.UtcNow;
        Assert.True(await manager.StartAsync(request));
        Assert.False(await manager.StartAsync(request));
        await publisher.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(publisher.Quotes);
        Assert.InRange(publisher.Quotes[0].Tick.LocalReceivedAtUtc, beforeReceipt, DateTimeOffset.UtcNow);
        Assert.NotEqual(publisher.Quotes[0].Tick.ReceiveTimestamp, publisher.Quotes[0].Tick.LocalReceivedAtUtc);
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

    [Fact]
    public async Task Statistics_supply_official_volume_and_open_interest_without_trade_double_counting()
    {
        var feed = new FakeChainFeed(
            Trade(1, 11_000_000_000),
            Statistic(2, 6, 100, ValueDate),
            Trade(3, 11_250_000_000),
            Statistic(4, 9, 500, ValueDate));
        using var lastPrices = new DatabentoLastPriceStore(ValueDate, 1);
        var publisher = new CapturingChainPublisher(2);
        var state = new OptionChainStateStore();
        await using var manager = new DatabentoOptionChainSessionManager(
            new FakeFactory(feed),
            DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            new FakeAggregation(), lastPrices, new FakeEnricher(), publisher, state,
            pollTimeout: TimeSpan.FromMilliseconds(5));

        Assert.True(await manager.StartAsync(Request()));
        await publisher.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var key = new OptionChainSessionKey("ES-202609", Maturity);
        Assert.True(SpinWait.SpinUntil(() => state.TryGet(key, "ES20260918C6500", out var value)
            && value.OpenInterest == 500, TimeSpan.FromSeconds(2)));
        Assert.True(state.TryGet(key, "ES20260918C6500", out var observed));
        Assert.Equal(100, observed.SessionVolume);
        Assert.Equal(500, observed.OpenInterest);
        Assert.True(observed.SessionVolumeOfficial);

        await manager.StopAsync("ES-202609", Maturity);
    }

    [Fact]
    public async Task Four_individual_legs_use_four_connections_and_release_only_the_selected_leg()
    {
        var feeds = Enumerable.Range(0, 4).Select(_ => new FakeChainFeed()).ToArray();
        var factory = new FakeFactory(feeds);
        using var prices = new DatabentoLastPriceStore(ValueDate, 8);
        var state = new OptionChainStateStore();
        await using var manager = new DatabentoOptionChainSessionManager(factory,
            DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            new FakeAggregation(), prices, new FakeEnricher(), new CapturingChainPublisher(0), state);
        for (var index = 0; index < 4; index++)
        {
            var original = Request();
            var id = $"leg-{index}";
            var definition = original.Routes[0].Definition with
            { Instrument = new InstrumentKey(7, (uint)(43 + index)), RawSymbol = $"fixture-{index}" };
            var request = original with
            {
                OptionContractId = id,
                Routes = [new() { FuturesOptionContractId = id, Definition = definition }],
                Subscription = original.Subscription with { ResolvedContracts = [definition] }
            };
            Assert.True(await manager.StartAsync(request));
            Assert.False(await manager.StartAsync(request));
        }
        Assert.Equal(4, factory.CreateCount);
        Assert.Equal(4, manager.ActiveSessionCount);
        Assert.False(await manager.StopAsync("ES-202609", Maturity));
        Assert.True(await manager.StopAsync("ES-202609", Maturity, "leg-1"));
        Assert.Equal(3, manager.ActiveSessionCount);
        Assert.True(feeds[1].Disposed);
        Assert.False(feeds[0].Disposed);
        Assert.False(feeds[2].Disposed);
        Assert.False(feeds[3].Disposed);
        Assert.Single(state.GetSession(new("ES-202609", Maturity, "leg-0")));
        await manager.DisposeAsync();
        Assert.Equal(0, manager.ActiveSessionCount);
        Assert.All(feeds, feed => Assert.True(feed.Disposed));
    }

    [Fact]
    public async Task Cache_and_manual_owners_share_quotes_without_duplicate_publication()
    {
        var feed = new FakeChainFeed(Quote(1, 10_000_000_000, 12_000_000_000));
        var factory = new FakeFactory(feed);
        using var prices = new DatabentoLastPriceStore(ValueDate, 1);
        var state = new OptionChainStateStore();
        var publisher = new CapturingChainPublisher(1);
        await using var manager = Manager(factory, prices, state, publisher);
        await manager.StartAsync(Request());
        await publisher.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await manager.StartAsync(Request() with { ConnectionScopeId = "cache" });
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(2, manager.ActiveSessionCount);
        Assert.True(state.TryGet(new("ES-202609", Maturity, "", "cache"), "ES20260918C6500", out var quote));
        Assert.NotNull(quote.Quote);
        Assert.Single(publisher.Quotes);
        await manager.StopAsync("ES-202609", Maturity);
        Assert.Equal(0, feed.StopCount);
        feed.Push(Quote(2, 11_000_000_000, 13_000_000_000));
        await WaitUntil(() => state.TryGet(new("ES-202609", Maturity, "", "cache"), "ES20260918C6500", out var q)
            && q.Quote?.Tick.SourceSequence == 2);
        await manager.StopAsync("ES-202609", Maturity, "", "cache");
        Assert.Equal(1, feed.StopCount);
    }

    [Fact]
    public async Task Different_expiries_replace_with_union_after_stopping_original_and_preserve_leg_feed()
    {
        var original = new FakeChainFeed(); var leg = new FakeChainFeed();
        var expanded = new FakeChainFeed(Quote(2, 11_000_000_000, 13_000_000_000));
        var factory = new FakeFactory(original, leg, expanded);
        using var prices = new DatabentoLastPriceStore(ValueDate, 4);
        var state = new OptionChainStateStore();
        await using var manager = Manager(factory, prices, state, new CapturingChainPublisher(1));
        await manager.StartAsync(Request());
        await manager.StartAsync(Request() with { OptionContractId = "ES20260918C6500" });
        expanded.BeforeStart = () => Assert.True(original.Disposed);
        var next = NextExpiry(); await manager.StartAsync(next);
        Assert.Equal(2, expanded.Subscription!.ResolvedContracts.Count);
        Assert.True(expanded.Subscription.MultipleMaturities);
        Assert.Equal(0, leg.StopCount);
        await WaitUntil(() => state.TryGet(new("ES-202609", Maturity), "ES20260918C6500", out var q) && q.Quote is not null);
        await manager.StopAsync("ES-202609", next.Subscription.MaturityDate, "", next.ConnectionScopeId);
        Assert.Equal(0, expanded.StopCount);
        await manager.StopAsync("ES-202609", Maturity);
        Assert.Equal(1, expanded.StopCount);
        Assert.Equal(0, leg.StopCount);
    }

    [Fact]
    public async Task Failed_expansion_restores_previous_subscription_and_removes_failed_owner()
    {
        var original = new FakeChainFeed(); var failed = new FakeChainFeed { FailStart = true };
        var restored = new FakeChainFeed(Quote(3, 10_000_000_000, 12_000_000_000));
        using var prices = new DatabentoLastPriceStore(ValueDate, 4);
        var state = new OptionChainStateStore();
        await using var manager = Manager(new FakeFactory(original, failed, restored), prices, state, new CapturingChainPublisher(1));
        await manager.StartAsync(Request());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync(NextExpiry()));
        Assert.True(original.Disposed); Assert.True(failed.Disposed);
        Assert.Equal(1, manager.ActiveSessionCount);
        Assert.Single(restored.Subscription!.ResolvedContracts);
        await WaitUntil(() => state.TryGet(new("ES-202609", Maturity), "ES20260918C6500", out var q) && q.Quote is not null);
        Assert.Empty(state.GetSession(new("ES-202609", Maturity.AddDays(7), "", "next")));
    }

    private static DatabentoOptionChainSessionManager Manager(FakeFactory factory,
        DatabentoLastPriceStore prices, OptionChainStateStore state, CapturingChainPublisher publisher) => new(
        factory, DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
        new FakeAggregation(), prices, new FakeEnricher(), publisher, state, pollTimeout: TimeSpan.FromMilliseconds(5));

    private static DatabentoOptionChainSessionRequest NextExpiry()
    {
        var request = Request();
        var route = request.Routes[0] with
        {
            FuturesOptionContractId = "next-option",
            Definition = request.Routes[0].Definition with
            { Instrument = new InstrumentKey(7, 44), RawSymbol = "next-symbol", MaturityDate = Maturity.AddDays(7) }
        };
        return request with { ConnectionScopeId = "next", Routes = [route],
            Subscription = request.Subscription with { MaturityDate = Maturity.AddDays(7), ResolvedContracts = [route.Definition] } };
    }

    private static async Task WaitUntil(Func<bool> predicate)
    {
        var end = DateTime.UtcNow.AddSeconds(2);
        while (!predicate()) { if (DateTime.UtcNow >= end) throw new TimeoutException(); await Task.Delay(5); }
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

    private static MarketRecord64 Quote(
        uint sequence,
        long bid,
        long ask,
        ushort? publisherId = null) => new(
        new QuoteRecord64(
            new MarketRecordHeader32(
                Instrument.InstrumentId, publisherId ?? Instrument.PublisherId,
                MarketRecordKind.Quote, 0, sequence, sequence, sequence),
            bid, ask, 10, 11, 1, 1));

    private static MarketRecord64 Trade(
        uint sequence,
        long price,
        ushort? publisherId = null) => new(
        new TradeRecord64(
            new MarketRecordHeader32(
                Instrument.InstrumentId, publisherId ?? Instrument.PublisherId,
                MarketRecordKind.Trade, 0, sequence, sequence, sequence),
            price, 12, 1, 2, 0));

    private static MarketRecord64 Statistic(uint sequence, ushort type, long quantity, DateOnly referenceDate)
    {
        var reference = new DateTimeOffset(referenceDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var nanoseconds = checked((reference.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100L);
        return new(new StatisticsRecord64(
            new MarketRecordHeader32(Instrument.InstrumentId, Instrument.PublisherId,
                MarketRecordKind.Statistics, 0, nanoseconds, nanoseconds, sequence),
            long.MaxValue, quantity, nanoseconds, type, 0, 1, 0));
    }

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

    private sealed class FakeFactory(params FakeChainFeed[] feeds) : IDatabentoFeedFactory
    {
        internal int CreateCount { get; private set; }
        public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options)
        {
            CreateCount++;
            return feeds[Math.Min(CreateCount - 1, feeds.Length - 1)];
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
        internal OptionChainSubscription? Subscription { get; private set; }
        internal Action? BeforeStart { get; set; }
        internal bool FailStart { get; set; }
        internal int StopCount { get; private set; }
        internal bool Disposed { get; private set; }
        public ISynchronousBatchReader<MarketDataBatch64> Reader => _channel;
        public void Subscribe(OptionChainSubscription subscription, TimeSpan timeout) => Subscription = subscription;
        public void Start(TimeSpan timeout, Action<TimeSpan> startConsumer)
        {
            BeforeStart?.Invoke();
            if (FailStart) throw new InvalidOperationException("Injected provider failure");
            startConsumer(timeout);
            if (records.Length == 0) return;
            var batch = _channel.RentBatch(static () => false);
            foreach (var record in records) batch.Add(record);
            Assert.True(_channel.Publish(batch, static () => false));
        }
        internal void Push(MarketRecord64 record)
        {
            var batch = _channel.RentBatch(static () => false); batch.Add(record);
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
