using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class TickAggregationServiceTests
{
    [Fact]
    public async Task Stream_owners_are_idempotent_and_reference_count_routes()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new RunningFeed(instrument);
        using var lastPrices = new DatabentoLastPriceStore(valueDate, 1);
        var routes = new CapturingStreamRoutes();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument, CreateDetails(valueDate, instrument)),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = valueDate },
            lastPrices: lastPrices,
            streamRoutes: routes);

        await service.StartAsync();
        var spreadA = new TickerStreamOwner("Spread", "A", "long");
        var spreadB = new TickerStreamOwner("Spread", "B", "short");
        Assert.True(service.StartTickDataStream(spreadA, "ESU6"));
        Assert.False(service.StartTickDataStream(spreadA, "ESU6"));
        Assert.True(service.StartTickDataStream(spreadB, "ESU6"));
        Assert.Equal(1, routes.Activations);
        Assert.True(service.IsTickDataStreamActive("ESU6"));

        Assert.True(service.StopTickDataStream(spreadA, "ESU6"));
        Assert.Equal(0, routes.Deactivations);
        Assert.True(service.IsTickDataStreamActive("ESU6"));

        Assert.True(service.StopTickDataStream(spreadB, "ESU6"));
        Assert.Equal(1, routes.Deactivations);
        Assert.False(service.IsTickDataStreamActive("ESU6"));
        await service.StopAsync();
    }

    [Fact]
    public async Task Hot_cache_combines_decimal_trade_and_quote_with_provider_and_domain_identity()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FakeFeed(
            instrument,
            Quote(instrument, 1, 5_000_000_000, 5_100_000_000),
            Trade(instrument, 2, 5_050_000_000));
        using var lastPrices = new DatabentoLastPriceStore(valueDate, 1);
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument, CreateDetails(valueDate, instrument)),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = valueDate },
            lastPrices: lastPrices,
            streamRoutes: new CapturingStreamRoutes());

        await service.StartAsync();
        FuturesMarketPriceSnapshot snapshot = default;
        Assert.True(SpinWait.SpinUntil(
            () => service.TryGetLastTickPrice("ESU6", out snapshot)
                && snapshot.Trade is not null
                && snapshot.Quote is not null,
            TimeSpan.FromSeconds(2)));

        Assert.Equal("ESU6", snapshot.ContractId);
        Assert.Equal(42u, snapshot.InstrumentId);
        Assert.Equal(7, snapshot.PublisherId);
        Assert.Equal(5.05m, snapshot.Trade!.Value.LastPrice);
        Assert.Equal(12u, snapshot.Trade.Value.LastSize);
        Assert.Equal(5m, snapshot.Quote!.Value.BidPrice);
        Assert.Equal(5.1m, snapshot.Quote.Value.AskPrice);
        Assert.Equal(10u, snapshot.Quote.Value.BidSize);
        Assert.Equal(11u, snapshot.Quote.Value.AskSize);
        await service.StopAsync();
    }

    [Fact]
    public async Task Last_price_cache_returns_the_realtime_snapshot_without_a_reader_lease()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FakeFeed(
            instrument,
            Quote(instrument, 1, 5_000_000_000, 5_100_000_000),
            Trade(instrument, 2, 5_050_000_000));
        var publisher = new CapturingPublisher();
        var routes = new CapturingStreamRoutes();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            publisher,
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions
            {
                Dataset = "GLBX.MDP3",
                DefinitionDate = valueDate
            },
            streamRoutes: routes);

        Assert.False(service.TryGetLastTickPrice("ESU6", out _));
        await service.StartAsync();

        var realtimeEvent = await publisher.MarketPrice.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        Assert.True(service.TryGetLastTickPrice("ESU6", out var snapshot));

        Assert.Equal(realtimeEvent.Price, snapshot);
        Assert.Equal(ActorType.Realtime, realtimeEvent.Subject.ActorType);
        Assert.Equal(FuturesMarketPriceUpdatedRealtimeEvent.Actor, realtimeEvent.Subject.Name);
        Assert.Equal(FuturesMarketPriceUpdatedRealtimeEvent.Verb, realtimeEvent.Subject.Verb);
        Assert.Equal("ESU6", snapshot.ContractId);
        Assert.Equal(42u, snapshot.InstrumentId);
        Assert.Equal((ushort)7, snapshot.PublisherId);
        Assert.Equal(5.05m, snapshot.Trade!.Value.LastPrice);
        Assert.Equal(5m, snapshot.Quote!.Value.BidPrice);
        Assert.Equal(5.1m, snapshot.Quote.Value.AskPrice);
        Assert.Equal(0, routes.Activations);
        Assert.Equal(0, routes.Deactivations);
        Assert.False(service.TryGetLastTickPrice("NQU6", out _));

        await service.StopAsync();
        Assert.True(service.TryGetLastTickPrice("ESU6", out _));
    }

    [Fact]
    public async Task Last_price_cache_and_realtime_event_reject_older_trade_and_quote_updates()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FakeFeed(
            instrument,
            Quote(instrument, 3, 5_000_000_000, 5_100_000_000),
            Quote(instrument, 2, 4_000_000_000, 4_100_000_000),
            Trade(instrument, 5, 5_050_000_000),
            Trade(instrument, 4, 4_050_000_000));
        var publisher = new CapturingPublisher();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            publisher,
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions
            {
                Dataset = "GLBX.MDP3",
                DefinitionDate = valueDate
            });

        await service.StartAsync();
        Assert.True(SpinWait.SpinUntil(
            () => service.GetMetrics().EmittedTradeEvents == 2,
            TimeSpan.FromSeconds(2)));
        Assert.True(service.TryGetLastTickPrice("ESU6", out var snapshot));

        Assert.Equal(5.05m, snapshot.Trade!.Value.LastPrice);
        Assert.Equal(5m, snapshot.Quote!.Value.BidPrice);
        Assert.Single(publisher.MarketPrices);
        Assert.Equal(snapshot, publisher.MarketPrices[0].Price);

        await service.StopAsync();
    }

    [Fact]
    public async Task Realtime_publication_failure_does_not_stop_cache_or_durable_ingestion()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FakeFeed(
            instrument,
            Trade(instrument, 1, 5_000_000_000),
            Trade(instrument, 2, 5_100_000_000));
        var publisher = new RejectRealtimePublisher();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            publisher,
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions
            {
                Dataset = "GLBX.MDP3",
                DefinitionDate = valueDate
            });

        await service.StartAsync();
        Assert.True(SpinWait.SpinUntil(
            () => service.GetMetrics().EmittedTradeEvents == 2,
            TimeSpan.FromSeconds(2)));

        Assert.True(service.TryGetLastTickPrice("ESU6", out var snapshot));
        Assert.Equal(5.1m, snapshot.Trade!.Value.LastPrice);
        Assert.Equal(2, publisher.DurableTradeCount);
        Assert.Equal(2, service.GetMetrics().PublicationFailures);

        await service.StopAsync();
    }

    [Fact]
    public async Task Released_stream_owner_can_reacquire_after_final_deactivation()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new RunningFeed(instrument);
        using var lastPrices = new DatabentoLastPriceStore(valueDate, 1);
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = valueDate },
            lastPrices: lastPrices,
            streamRoutes: new CapturingStreamRoutes());

        await service.StartAsync();
        var owner = new TickerStreamOwner("Spread", "A", "underlying");
        Assert.True(service.StartTickDataStream(owner, "ESU6"));
        Assert.True(service.StopTickDataStream(owner, "ESU6"));
        Assert.False(service.IsTickDataStreamActive("ESU6"));
        Assert.True(service.StartTickDataStream(owner, "ESU6"));
        Assert.True(service.IsTickDataStreamActive("ESU6"));
        Assert.True(service.StopTickDataStream(owner, "ESU6"));
        await service.StopAsync();
    }

    [Fact]
    public async Task Concurrent_duplicate_start_registers_one_owner_and_one_route_activation()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new RunningFeed(instrument);
        using var lastPrices = new DatabentoLastPriceStore(valueDate, 1);
        var routes = new CapturingStreamRoutes();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = valueDate },
            lastPrices: lastPrices,
            streamRoutes: routes);

        await service.StartAsync();
        var owner = new TickerStreamOwner("Spread", "A", "underlying");
        var starts = await Task.WhenAll(Enumerable.Range(0, 32).Select(
            _ => Task.Run(() => service.StartTickDataStream(owner, "ESU6"))));

        Assert.Single(starts, started => started);
        Assert.Equal(1, routes.Activations);
        Assert.True(service.StopTickDataStream(owner, "ESU6"));
        Assert.Equal(1, routes.Deactivations);
        await service.StopAsync();
    }

    [Fact]
    public async Task Service_stop_clears_every_outstanding_stream_owner()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new RunningFeed(instrument);
        using var lastPrices = new DatabentoLastPriceStore(valueDate, 1);
        var routes = new CapturingStreamRoutes();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = valueDate },
            lastPrices: lastPrices,
            streamRoutes: routes);

        await service.StartAsync();
        var owner = new TickerStreamOwner("Spread", "A", "underlying");
        Assert.True(service.StartTickDataStream(owner, "ESU6"));
        await service.StopAsync();

        Assert.False(service.IsTickDataStreamActive("ESU6"));
        Assert.Equal(1, routes.Deactivations);
    }

    [Fact]
    public async Task Incomplete_feed_stop_returns_without_waiting_for_worker_and_can_be_retried()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 42);
        using var feed = new RetryStopFeed(instrument);
        var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions
            {
                Dataset = "GLBX.MDP3",
                DefinitionDate = valueDate,
                FeedStopTimeout = TimeSpan.FromMilliseconds(50)
            });

        await service.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(
            () => service.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.IsType<FeedStopDrainIncompleteException>(failure.InnerException);
        Assert.True(service.IsRunning);

        await service.StopAsync();
        Assert.False(service.IsRunning);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task Futures_option_ticks_use_the_same_persistence_pipeline_and_hot_store()
    {
        var valueDate = new DateOnly(2026, 8, 10);
        var instrument = new InstrumentKey(7, 99);
        using var feed = new FakeFeed(instrument,
            Quote(instrument, 1, 10_000_000_000, 12_000_000_000),
            Trade(instrument, 2, 11_000_000_000));
        using var lastPrices = new DatabentoLastPriceStore(valueDate, 1);
        var livePublisher = new CapturingLivePublisher();
        var liveRouter = new TickLiveRouter(livePublisher);
        Assert.True(liveRouter.Activate("ESU6 C6500"));
        await using var service = new TickAggregationService(
            feed,
            new AssetMappingProvider(instrument, "ESU6 C6500", AssetTypeId.FuturesOption),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new FixedValueDateProvider(valueDate),
            new TickAggregationOptions
            {
                Dataset = "GLBX.MDP3",
                DefinitionDate = valueDate
            },
            lastPrices: lastPrices,
            liveRouter: liveRouter);

        await service.StartAsync();
        await service.StopAsync();

        var status = service.GetContractStatus("ESU6 C6500");
        Assert.Equal(AssetTypeId.FuturesOption, status.AssetTypeId);
        Assert.True(status.ContractConfigured);
        Assert.False(service.IsTickDataStreamActive("ESU6 C6500"));
        Assert.True(service.TryGetLastOptionTickPrice("ESU6 C6500", out var optionPrice));
        Assert.Equal(11m, optionPrice.Price.Trade!.Value.LastPrice);
        Assert.Equal(10m, optionPrice.Price.Quote!.Value.BidPrice);
        Assert.Equal(12m, optionPrice.Price.Quote.Value.AskPrice);
        var reader = lastPrices.GetFuturesOptionReader("ESU6 C6500", valueDate);
        Assert.True(reader.TryGetLastQuote(out var quote));
        Assert.True(quote.TryGetMidpoint(out var midpoint));
        Assert.Equal(11m, midpoint);
        Assert.True(reader.TryGetLastTrade(out var trade));
        Assert.Equal(11m, trade.Price);
        Assert.Single(livePublisher.Quotes);
        Assert.Single(livePublisher.Trades);
        Assert.Equal(AssetTypeId.FuturesOption, livePublisher.Quotes[0].AssetTypeId);
    }

    [Fact]
    public async Task Ticker_status_requires_live_service_and_registered_contract()
    {
        var instrument = new InstrumentKey(7, 42);
        using var feed = new RunningFeed(instrument);
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            new CapturingPublisher(),
            new TickQuoteBufferPool(),
            new UtcTickValueDateProvider(),
            new TickAggregationOptions
            {
                Dataset = "GLBX.MDP3",
                DefinitionDate = new DateOnly(2026, 8, 7)
            });

        var stopped = service.GetTickerStatus("ESU6");
        Assert.False(stopped.ServiceRunning);
        Assert.False(stopped.TickerConfigured);
        Assert.False(stopped.TickerRunning);

        await service.StartAsync();

        var running = service.GetTickerStatus("ESU6");
        Assert.True(running.ServiceRunning);
        Assert.True(running.TickerConfigured);
        Assert.True(running.TickerRunning);

        var unknown = service.GetTickerStatus("NQU6");
        Assert.True(unknown.ServiceRunning);
        Assert.False(unknown.TickerConfigured);
        Assert.False(unknown.TickerRunning);

        await service.StopAsync();

        var stoppedAfterRun = service.GetTickerStatus("ESU6");
        Assert.False(stoppedAfterRun.ServiceRunning);
        Assert.True(stoppedAfterRun.TickerConfigured);
        Assert.False(stoppedAfterRun.TickerRunning);
    }

    [Fact]
    public async Task Trade_flushes_ticker_quotes_before_trade_with_shared_sequence()
    {
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FakeFeed(instrument,
            Quote(instrument, 1, 5_000_000_000, 5_100_000_000),
            Quote(instrument, 2, 5_010_000_000, 5_110_000_000),
            Trade(instrument, 3, 5_050_000_000));
        var publisher = new CapturingPublisher();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            publisher,
            new TickQuoteBufferPool(),
            new UtcTickValueDateProvider(),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = new DateOnly(2026, 8, 7) });

        await service.StartAsync();
        await service.StopAsync();

        Assert.Equal(["quote", "trade"], publisher.Order);
        Assert.Equal([1L, 2L], publisher.Sequences);
        Assert.Equal((ushort)2, publisher.QuoteCount);
        Assert.Equal(5.01m, publisher.SecondBid);
        var metrics = service.GetMetrics();
        Assert.Equal(2, metrics.SourceQuoteRecords);
        Assert.Equal(1, metrics.SourceTradeRecords);
        Assert.Equal(1, metrics.EmittedQuoteBatches);
        Assert.Equal(2, metrics.EmittedQuoteItems);
        Assert.Equal(1, metrics.EmittedTradeEvents);
        Assert.Equal(1, metrics.PartialQuoteFlushes);
        Assert.Equal(0, metrics.ServiceOwnedQuoteBuffers);
    }

    [Fact]
    public async Task Rejected_quote_publication_retains_lease_sequence_and_event_identity_for_stop_retry()
    {
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FakeFeed(instrument,
            Quote(instrument, 1, 5_000_000_000, 5_100_000_000),
            Trade(instrument, 2, 5_050_000_000));
        var publisher = new RejectFirstQuotePublisher();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            publisher,
            new TickQuoteBufferPool(),
            new UtcTickValueDateProvider(),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = new DateOnly(2026, 8, 7) });

        await service.StartAsync();
        await Assert.ThrowsAsync<AggregateException>(() => service.StopAsync().AsTask());

        Assert.Equal(2, publisher.QuoteAttempts.Count);
        Assert.Equal(publisher.QuoteAttempts[0], publisher.QuoteAttempts[1]);
        Assert.Equal(QuoteEmissionReason.TradeObserved, publisher.Reasons[0]);
        Assert.Equal(publisher.Reasons[0], publisher.Reasons[1]);
    }

    [Fact]
    public async Task Duplicate_out_of_order_and_gap_source_sequences_are_preserved_and_counted()
    {
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FakeFeed(instrument,
            Quote(instrument, 2, 5_000_000_000, 5_100_000_000),
            Quote(instrument, 2, 5_010_000_000, 5_110_000_000),
            Quote(instrument, 1, 5_020_000_000, 5_120_000_000),
            Trade(instrument, 4, 5_050_000_000));
        var publisher = new CapturingPublisher();
        await using var service = new TickAggregationService(
            feed,
            new MappingProvider(instrument),
            publisher,
            new TickQuoteBufferPool(),
            new UtcTickValueDateProvider(),
            new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = new DateOnly(2026, 8, 7) });

        await service.StartAsync();
        await service.StopAsync();

        var metrics = service.GetMetrics();
        Assert.Equal((ushort)3, publisher.QuoteCount);
        Assert.Equal(1, metrics.DuplicateSourceSequences);
        Assert.Equal(1, metrics.OutOfOrderSourceSequences);
        Assert.Equal(1, metrics.SourceSequenceGaps);
    }

    private static MarketRecord64 Quote(InstrumentKey key, uint sequence, long bid, long ask) => new(
        new QuoteRecord64(
            new MarketRecordHeader32(key.InstrumentId, key.PublisherId, MarketRecordKind.Quote, 0, sequence, sequence, sequence),
            bid, ask, 10, 11, 1, 1));

    private static MarketRecord64 Trade(InstrumentKey key, uint sequence, long price) => new(
        new TradeRecord64(
            new MarketRecordHeader32(key.InstrumentId, key.PublisherId, MarketRecordKind.Trade, 0, sequence, sequence, sequence),
            price, 12, 1, 2, 0));

    private sealed class MappingProvider(
        InstrumentKey instrument,
        TickerContractDetails? details = null) : ITickContractMappingProvider
    {
        public bool TryGetMapping(string dataset, DateOnly definitionDate, InstrumentKey key, out TickContractMapping mapping)
        {
            mapping = new TickContractMapping(
                dataset,
                definitionDate,
                key.PublisherId,
                key.InstrumentId,
                "ESU6",
                AssetTypeId.Futures,
                details);
            return key == instrument;
        }
    }

    private static TickerContractDetails CreateDetails(
        DateOnly valueDate,
        InstrumentKey instrument) => new()
    {
        ContractId = "ESU6",
        InstrumentId = instrument.InstrumentId,
        PublisherId = instrument.PublisherId,
        AssetTypeId = AssetTypeId.Futures,
        Dataset = "GLBX.MDP3",
        DefinitionDate = valueDate,
        ProviderContractId = "ESU6",
        Ticker = "ES",
        LocalSymbol = "ESU6",
        SecurityType = "FUT",
        Currency = "USD",
        Exchange = "CME",
        ContractMultiplier = 50m,
        MaturityDate = new DateOnly(2026, 9, 18),
        IsCurrentlyTraded = true
    };

    private sealed class CapturingStreamRoutes : ITickerStreamRouteController
    {
        public int Activations { get; private set; }
        public int Deactivations { get; private set; }
        public void Activate(TickContractMapping mapping) => Activations++;
        public void Deactivate(TickContractMapping mapping) => Deactivations++;
    }

    private sealed class AssetMappingProvider(
        InstrumentKey instrument,
        string contractId,
        AssetTypeId assetTypeId) : ITickContractMappingProvider
    {
        public bool TryGetMapping(
            string dataset,
            DateOnly definitionDate,
            InstrumentKey key,
            out TickContractMapping mapping)
        {
            mapping = new TickContractMapping(
                dataset, definitionDate, key.PublisherId, key.InstrumentId,
                contractId, assetTypeId);
            return key == instrument;
        }
    }

    private sealed class FixedValueDateProvider(DateOnly valueDate) : ITickValueDateProvider
    {
        public DateOnly GetValueDate(DateTime timestampUtc) => valueDate;
    }

    private sealed class CapturingPublisher : ITickAggregationEventPublisher
    {
        public List<string> Order { get; } = [];
        public List<long> Sequences { get; } = [];
        public List<FuturesMarketPriceUpdatedRealtimeEvent> MarketPrices { get; } = [];
        public TaskCompletionSource<FuturesMarketPriceUpdatedRealtimeEvent> MarketPrice { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ushort QuoteCount { get; private set; }
        public decimal? SecondBid { get; private set; }
        public bool IsRunning { get; private set; }
        public ValueTask StartAsync() { IsRunning = true; return ValueTask.CompletedTask; }
        public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent e)
        {
            MarketPrices.Add(e);
            MarketPrice.TrySetResult(e);
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent e)
        {
            Order.Add("trade"); Sequences.Add(e.TickDataId.SequenceId); return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent e, ITickQuoteBufferLease lease)
        {
            Order.Add("quote"); Sequences.Add(e.TickDataId.SequenceId);
            QuoteCount = e.QuoteCount; SecondBid = e.QuoteData.Buffer[1].BidPrice;
            lease.Dispose();
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync() { IsRunning = false; return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() => StopAsync();
    }

    private sealed class CapturingLivePublisher : ITickLiveEventPublisher
    {
        public List<LiveTickQuoteServiceEvent> Quotes { get; } = [];
        public List<LiveTickTradeServiceEvent> Trades { get; } = [];
        public ValueTask PublishAsync(LiveTickQuoteServiceEvent @event)
        {
            Quotes.Add(@event);
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(LiveTickTradeServiceEvent @event)
        {
            Trades.Add(@event);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RejectFirstQuotePublisher : ITickAggregationEventPublisher
    {
        public List<(Guid EventId, Guid CommandId, long Sequence)> QuoteAttempts { get; } = [];
        public List<QuoteEmissionReason> Reasons { get; } = [];
        public bool IsRunning { get; private set; }
        public ValueTask StartAsync() { IsRunning = true; return ValueTask.CompletedTask; }
        public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent e) =>
            ValueTask.CompletedTask;
        public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent e) => ValueTask.CompletedTask;
        public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent e, ITickQuoteBufferLease lease)
        {
            QuoteAttempts.Add((e.Id, e.CommandId, e.TickDataId.SequenceId));
            Reasons.Add(e.EmissionReason);
            if (QuoteAttempts.Count == 1)
                throw new IOException("Synthetic bounded-channel rejection.");
            lease.Dispose();
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync() { IsRunning = false; return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() => StopAsync();
    }

    private sealed class RejectRealtimePublisher : ITickAggregationEventPublisher
    {
        public int DurableTradeCount { get; private set; }
        public bool IsRunning { get; private set; }
        public ValueTask StartAsync()
        {
            IsRunning = true;
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent e) =>
            ValueTask.FromException(new IOException("Synthetic Core NATS failure."));
        public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent e)
        {
            DurableTradeCount++;
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(
            FuturesTickQuoteDataChangedEvent e,
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

    private sealed class FakeFeed : IDatabentoTickerFeed
    {
        private readonly InstrumentKey _instrument;
        private readonly MarketRecord64[] _records;
        private readonly BoundedBatchChannel _channel = new(4, 64);
        private bool _leased;
        public FakeFeed(InstrumentKey instrument, params MarketRecord64[] records) { _instrument = instrument; _records = records; }
        public void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout) { }
        public void Start(TimeSpan timeout)
        {
            var batch = _channel.RentBatch(static () => false);
            foreach (var record in _records) batch.Add(record);
            Assert.True(_channel.Publish(batch, static () => false));
            _channel.Complete();
        }
        public void Stop(TimeSpan timeout) => _channel.Complete();
        public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument) => _channel;
        public IMultiplexedTickerBatchReader GetMultiplexedReader()
        {
            if (_leased) throw new InvalidOperationException();
            _leased = true;
            return new MultiplexedTickerBatchReader([(_instrument, _channel)], () => _leased = false);
        }
        public IReadOnlyList<TickerInstrumentRegistration> GetInstruments() =>
            [new TickerInstrumentRegistration("ES", "ESU6", _instrument)];
        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose() => _channel.Complete();
    }

    private sealed class RunningFeed(InstrumentKey instrument) : IDatabentoTickerFeed
    {
        private readonly BoundedBatchChannel _channel = new(4, 64);
        private bool _leased;

        public void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout) { }
        public void Start(TimeSpan timeout) { }
        public void Stop(TimeSpan timeout) => _channel.Complete();
        public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey key) => _channel;
        public IMultiplexedTickerBatchReader GetMultiplexedReader()
        {
            if (_leased) throw new InvalidOperationException();
            _leased = true;
            return new MultiplexedTickerBatchReader([(instrument, _channel)], () => _leased = false);
        }
        public IReadOnlyList<TickerInstrumentRegistration> GetInstruments() =>
            [new TickerInstrumentRegistration("ES", "ESU6", instrument)];
        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose() => _channel.Complete();
    }

    private sealed class RetryStopFeed(InstrumentKey instrument) : IDatabentoTickerFeed
    {
        private readonly BoundedBatchChannel _channel = new(4, 64);
        private bool _leased;
        private int _stopAttempts;

        public void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout) { }
        public void Start(TimeSpan timeout) { }
        public void Stop(TimeSpan timeout)
        {
            if (Interlocked.Increment(ref _stopAttempts) == 1)
            {
                throw new FeedStopDrainIncompleteException(
                    "Synthetic final drain did not complete before the deadline.");
            }
            _channel.Complete();
        }
        public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey key) => _channel;
        public IMultiplexedTickerBatchReader GetMultiplexedReader()
        {
            if (_leased) throw new InvalidOperationException();
            _leased = true;
            return new MultiplexedTickerBatchReader([(instrument, _channel)], () => _leased = false);
        }
        public IReadOnlyList<TickerInstrumentRegistration> GetInstruments() =>
            [new TickerInstrumentRegistration("ES", "ESU6", instrument)];
        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose() => _channel.Complete();
    }
}
