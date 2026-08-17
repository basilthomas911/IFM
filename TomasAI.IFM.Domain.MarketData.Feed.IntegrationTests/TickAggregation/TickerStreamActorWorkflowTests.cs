using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using OptionTradeHandler = TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.FuturesTickTradeDataInserted;
using FuturesTradeHandler = TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.FuturesTickTradeDataInserted;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.TickAggregation;

/// <summary>
/// Exercises real TickAggregation stream ownership and hot-cache prices with downstream domain handlers.
/// </summary>
public sealed class TickerStreamActorWorkflowTests
{
    private static readonly DateOnly ValueDate = new(2026, 8, 14);

    [Fact]
    public async Task Realtime_futures_trade_uses_active_stream_and_exact_decimal_trade()
    {
        const string contractId = "VX";
        var instrument = new InstrumentKey(7, 42);
        using var feed = new FiniteFeed(
            instrument,
            Quote(instrument, 1, 20.10m, 20.20m),
            Trade(instrument, 2, 20.15m, 17));
        using var lastPrices = new DatabentoLastPriceStore(ValueDate, 1);
        var publisher = new CapturingPublisher();
        await using var aggregation = CreateAggregation(
            feed,
            lastPrices,
            publisher,
            instrument,
            CreateDetails(contractId, instrument, AssetTypeId.Futures));
        await aggregation.StartAsync();

        var marketPriceEvent = await publisher.MarketPrice.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        aggregation.TryGetLastTickPrice(contractId, out var marketPrice)
            .Should().BeTrue();
        marketPrice.Should().Be(marketPriceEvent.Price);
        marketPrice.Trade!.Value.LastPrice.Should().Be(20.15m);
        marketPrice.Quote!.Value.BidPrice.Should().Be(20.10m);

        var marketDataApi = CreateMarketDataApi(aggregation);
        var logger = Substitute.For<ILogger<global::TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor.FuturesTickDataEventActor>>();
        marketDataApi.GetFuturesContractAsync(contractId).Returns(new FuturesContractV2ReadModel(
            contractId, contractId, "VX", contractId, "FUT", "USD", "CME", "50",
            new DateOnly(2026, 9, 18), true));
        var blackboard = new BlackboardService(
            Substitute.For<IRedisCache>(),
            new SystemTextJsonSerializer());
        var owner = new TickerStreamOwner("IntegrationWorkflow", "futures", "underlying");
        aggregation.StartTickDataStream(owner, contractId).Should().BeTrue();
        var changed = await publisher.Trade.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var inserted = ToInserted(changed);
        var projector = CreateEodProjector();

        var handled = await FuturesTradeHandler.ExecuteAsync(
            inserted,
            Substitute.For<IEventActorContext>(),
            marketDataApi,
            blackboard,
            Substitute.For<IStatusConsoleWriter>(),
            projector,
            logger);

        handled.Should().BeTrue();
        var emitted = projector.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IRealtimeProjector<FuturesEodDataRealtimeActor>.ProcessRealtimeEventAsync))
            .GetArguments()[0]
            .Should().BeOfType<VixFuturesEodDataInsertedEvent>().Which;
        emitted.VixFuturesTickData.ContractId.Should().Be(contractId);
        emitted.VixFuturesTickData.Price.Should().Be(20.15m);
        emitted.VixFuturesTickData.Size.Should().Be(17);
        emitted.VixFuturesTickData.TickId.Should().Be(2);

        aggregation.StopTickDataStream(owner, contractId).Should().BeTrue();
        await aggregation.StopAsync();
    }

    [Fact]
    public async Task Es_trade_defers_until_vix_eod_is_available_then_projects_realtime_eod()
    {
        const string esContractId = "ES20260918";
        const string vixContractId = "VX20260916";
        var esContract = new FuturesContractV2ReadModel(
            esContractId, esContractId, "ES", "ESU6", "FUT", "USD", "CME", "50",
            new DateOnly(2026, 9, 18), true);
        var vixContract = new FuturesContractV2ReadModel(
            vixContractId, vixContractId, "VX", "VXU6", "FUT", "USD", "CFE", "1000",
            new DateOnly(2026, 9, 16), true);
        var marketDataApi = Substitute.For<IMarketDataApi>();
        marketDataApi.IsTickDataStreamActive(Arg.Any<string>()).Returns(true);
        marketDataApi.GetFuturesContractAsync(esContractId).Returns(esContract);
        marketDataApi.GetFuturesContractAsync(vixContractId).Returns(vixContract);
        var redis = new MemoryRedisCache();
        var blackboard = new BlackboardService(redis, new SystemTextJsonSerializer());
        var logger = Substitute.For<ILogger<global::TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor.FuturesTickDataEventActor>>();

        var context = Substitute.For<IEventActorContext>();
        var currentEod = new FuturesEodDataV2ReadModel(
            esContractId, ValueDate, "ES", 5400m, 5450m, 5380m, 5425m, 1000,
            0.1, 0.01, 54.25, 5500, 5425, 5350,
            MarketDirectionType.NeutralUp, MarketVolatilityType.Normal,
            PriceDirectionType.Rising, PriceVolatilityType.Falling);
        context.RequestAsync<FuturesEodDataV2ReadModel, GetFuturesEodDataQuery>(
                Arg.Any<GetFuturesEodDataQuery>())
            .Returns(new ServiceOk<FuturesEodDataV2ReadModel>(currentEod));
        context.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(
                Arg.Any<GetVixFuturesEodDataQuery>())
            .Returns(new ServiceOk<VixFuturesEodDataReadModel[]>([]));
        context.RequestAsync<FuturesEodDataV2ReadModel[], GetFuturesEodDataByDateRangeQuery>(
                Arg.Any<GetFuturesEodDataByDateRangeQuery>())
            .Returns(new ServiceOk<FuturesEodDataV2ReadModel[]>([currentEod]));
        var normalCurve = new NormalCurveTableReadModel(
            Enumerable.Range(0, 101)
                .Select(index => new NormalCurveDataReadModel(index, index + 1))
                .ToArray());
        context.RequestAsync<NormalCurveTableReadModel, GetNormalCurveTableQuery>(
                Arg.Any<GetNormalCurveTableQuery>())
            .Returns(new ServiceOk<NormalCurveTableReadModel>(normalCurve));
        var projector = CreateEodProjector();
        var status = Substitute.For<IStatusConsoleWriter>();

        await FuturesTradeHandler.ExecuteAsync(
            CreateInsertedTrade(esContractId, 5450.25m, 10),
            context,
            marketDataApi,
            blackboard,
            status,
            projector,
            logger);
        projector.ReceivedCalls().Should().BeEmpty();

        blackboard.MarketDataFeed.VixFuturesContractId.Set(ValueDate, vixContractId);
        await FuturesTradeHandler.ExecuteAsync(
            CreateInsertedTrade(esContractId, 5450.50m, 11),
            context,
            marketDataApi,
            blackboard,
            status,
            projector,
            logger);
        projector.ReceivedCalls().Should().BeEmpty();

        await FuturesTradeHandler.ExecuteAsync(
            CreateInsertedTrade(vixContractId, 20.15m, 17),
            context,
            marketDataApi,
            blackboard,
            status,
            projector,
            logger);
        projector.ReceivedCalls().Select(call => call.GetArguments()[0])
            .Should().ContainSingle(argument => argument is VixFuturesEodDataInsertedEvent);

        blackboard.MarketDataFeed.VixFuturesEodData.Set(
            vixContractId,
            ValueDate,
            [new VixFuturesEodDataReadModel(
                vixContractId, ValueDate, 20m, 20.5m, 19.5m, 20.15m, 17)]);
        await FuturesTradeHandler.ExecuteAsync(
            CreateInsertedTrade(esContractId, 5451m, 12),
            context,
            marketDataApi,
            blackboard,
            status,
            projector,
            logger);

        projector.ReceivedCalls().Select(call => call.GetArguments()[0])
            .Should().ContainSingle(argument => argument is FuturesEodDataInsertedEvent);
    }

    [Fact]
    public async Task Realtime_option_trade_combines_exact_trade_with_aggregation_quote()
    {
        const string contractId = "ES20260918C6500";
        var instrument = new InstrumentKey(7, 99);
        using var feed = new FiniteFeed(
            instrument,
            Quote(instrument, 10, 12.25m, 12.75m),
            Trade(instrument, 11, 12.50m, 9));
        using var lastPrices = new DatabentoLastPriceStore(ValueDate, 1);
        var publisher = new CapturingPublisher();
        await using var aggregation = CreateAggregation(
            feed,
            lastPrices,
            publisher,
            instrument,
            CreateDetails(contractId, instrument, AssetTypeId.FuturesOption));
        await aggregation.StartAsync();

        var marketDataApi = CreateMarketDataApi(aggregation);
        var logger = Substitute.For<ILogger<global::TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor.FuturesOptionTickDataEventActor>>();
        var owner = new TickerStreamOwner("IntegrationWorkflow", "option", "long-call");
        aggregation.StartTickDataStream(owner, contractId).Should().BeTrue();
        marketDataApi.GetFuturesOptionContractAsync(contractId).Returns(
            new FuturesOptionContractReadModel(
                contractId, contractId, "ES", contractId, "FOP", "USD", "CME", "50",
                new DateOnly(2026, 9, 18), 6500d, "Call"));
        var changed = await publisher.Trade.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var inserted = ToInserted(changed);
        var eventApi = Substitute.For<IActorMarketDataFeedEventApi>();

        var handled = await OptionTradeHandler.ExecuteAsync(
            inserted,
            eventApi,
            marketDataApi,
            Substitute.For<IStatusConsoleWriter>(),
            logger);

        handled.Should().BeTrue();
        var emitted = eventApi.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IActorMarketDataFeedEventApi.SendOptionTradeTickPriceDataUpdatedEventAsync)
                && call.GetArguments().Length == 2)
            .GetArguments()[1]
            .Should().BeOfType<FuturesOptionTickDataV2ReadModel>().Which;
        emitted.ContractId.Should().Be(contractId);
        emitted.OptionPrice.Should().Be(12.50d);
        emitted.BidPrice.Should().Be(12.25d);
        emitted.AskPrice.Should().Be(12.75d);
        emitted.BidSize.Should().Be(10);
        emitted.AskSize.Should().Be(11);

        aggregation.StopTickDataStream(owner, contractId).Should().BeTrue();
        await aggregation.StopAsync();
    }

    private static TickAggregationService CreateAggregation(
        IDatabentoTickerFeed feed,
        IDatabentoLastPriceStore lastPrices,
        ITickAggregationEventPublisher publisher,
        InstrumentKey instrument,
        TickerContractDetails details) => new(
        feed,
        new MappingProvider(instrument, details),
        publisher,
        new TickQuoteBufferPool(),
        new FixedValueDateProvider(ValueDate),
        new TickAggregationOptions { Dataset = "GLBX.MDP3", DefinitionDate = ValueDate },
        lastPrices: lastPrices,
        streamRoutes: new NoOpStreamRoutes());

    private static IRealtimeProjector<FuturesEodDataRealtimeActor> CreateEodProjector()
    {
        var projector = Substitute.For<IRealtimeProjector<FuturesEodDataRealtimeActor>>();
        projector.ProcessRealtimeEventAsync(
                Arg.Any<IEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        return projector;
    }

    private static IMarketDataApi CreateMarketDataApi(TickAggregationService aggregation)
    {
        var api = Substitute.For<IMarketDataApi>();
        api.IsTickDataStreamActive(Arg.Any<string>())
            .Returns(call => aggregation.IsTickDataStreamActive(call.ArgAt<string>(0)));
        api.TryGetLastTickPrice(Arg.Any<string>(), out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call =>
            {
                var found = aggregation.TryGetLastTickPrice(
                    call.ArgAt<string>(0),
                    out var snapshot);
                call[1] = snapshot;
                return found;
            });
        api.TryGetLastOptionTickPrice(Arg.Any<string>(), out Arg.Any<OptionTickerPriceSnapshot>())
            .Returns(call =>
            {
                var found = aggregation.TryGetLastOptionTickPrice(
                    call.ArgAt<string>(0),
                    out var snapshot);
                call[1] = snapshot;
                return found;
            });
        return api;
    }

    private static TickerContractDetails CreateDetails(
        string contractId,
        InstrumentKey instrument,
        AssetTypeId assetTypeId) => new()
    {
        ContractId = contractId,
        InstrumentId = instrument.InstrumentId,
        PublisherId = instrument.PublisherId,
        AssetTypeId = assetTypeId,
        Dataset = "GLBX.MDP3",
        DefinitionDate = ValueDate,
        ProviderContractId = contractId,
        Ticker = assetTypeId == AssetTypeId.Futures ? "VX" : "ES",
        LocalSymbol = contractId,
        SecurityType = assetTypeId == AssetTypeId.Futures ? "FUT" : "FOP",
        Currency = "USD",
        Exchange = "CME",
        ContractMultiplier = 50m,
        MaturityDate = new DateOnly(2026, 9, 18),
        IsCurrentlyTraded = true,
        StrikePrice = assetTypeId == AssetTypeId.FuturesOption ? 6500m : null,
        OptionType = assetTypeId == AssetTypeId.FuturesOption ? "Call" : null,
        UnderlyingContractId = assetTypeId == AssetTypeId.FuturesOption ? "ES20260918" : null
    };

    private static FuturesTickTradeDataInsertedEvent ToInserted(
        FuturesTickTradeDataChangedEvent source) => new()
    {
        Subject = new ActorSubject(
            ActorType.Realtime,
            FuturesTickTradeDataInsertedEvent.Actor,
            FuturesTickTradeDataInsertedEvent.Verb,
            source.EntityId.Format()),
        Id = source.Id,
        EntityId = source.EntityId,
        EventId = source.EventId,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = source.EventSource,
        ReceivedOn = source.ReceivedOn,
        SchemaVersion = source.SchemaVersion,
        TickDataId = source.TickDataId,
        AssetTypeId = source.AssetTypeId,
        Dataset = source.Dataset,
        DefinitionDate = source.DefinitionDate,
        PublisherId = source.PublisherId,
        InstrumentId = source.InstrumentId,
        TradeData = source.TradeData
    };

    private static FuturesTickTradeDataInsertedEvent CreateInsertedTrade(
        string contractId,
        decimal price,
        uint size)
    {
        var entity = new TickDataEntityId(contractId, ValueDate, AssetTypeId.Futures);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
        return new FuturesTickTradeDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesTickTradeDataInsertedEvent.Actor,
                FuturesTickTradeDataInsertedEvent.Verb,
                entity.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entity,
            AggregateId = entity.Format(),
            EventSource = nameof(TickerStreamActorWorkflowTests),
            ReceivedOn = DateTime.UtcNow,
            TickDataId = new TickDataId(contractId, ValueDate, 1, DateTime.UtcNow),
            AssetTypeId = AssetTypeId.Futures,
            Dataset = "GLBX.MDP3",
            DefinitionDate = ValueDate,
            PublisherId = 7,
            InstrumentId = 42,
            TradeData = new FuturesTickTradeData(
                1,
                timestamp,
                timestamp,
                0,
                Scale(price),
                price,
                size,
                1,
                2,
                0)
        };
    }

    private static MarketRecord64 Quote(
        InstrumentKey key,
        uint sequence,
        decimal bid,
        decimal ask) => new(new QuoteRecord64(
        Header(key, MarketRecordKind.Quote, sequence),
        Scale(bid),
        Scale(ask),
        10,
        11,
        1,
        1));

    private static MarketRecord64 Trade(
        InstrumentKey key,
        uint sequence,
        decimal price,
        uint size) => new(new TradeRecord64(
        Header(key, MarketRecordKind.Trade, sequence),
        Scale(price),
        size,
        1,
        2,
        0));

    private static MarketRecordHeader32 Header(
        InstrumentKey key,
        MarketRecordKind kind,
        uint sequence)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
        return new MarketRecordHeader32(
            key.InstrumentId,
            key.PublisherId,
            kind,
            0,
            timestamp,
            timestamp,
            sequence);
    }

    private static long Scale(decimal price) =>
        decimal.ToInt64(price * 1_000_000_000m);

    private sealed class MappingProvider(
        InstrumentKey instrument,
        TickerContractDetails details) : ITickContractMappingProvider
    {
        public bool TryGetMapping(
            string dataset,
            DateOnly definitionDate,
            InstrumentKey key,
            out TickContractMapping mapping)
        {
            mapping = new TickContractMapping(
                dataset,
                definitionDate,
                key.PublisherId,
                key.InstrumentId,
                details.ContractId,
                details.AssetTypeId,
                details);
            return key == instrument;
        }
    }

    private sealed class FixedValueDateProvider(DateOnly valueDate) : ITickValueDateProvider
    {
        public DateOnly GetValueDate(DateTime timestampUtc) => valueDate;
    }

    private sealed class NoOpStreamRoutes : ITickerStreamRouteController
    {
        public void Activate(TickContractMapping mapping) { }
        public void Deactivate(TickContractMapping mapping) { }
    }

    private sealed class CapturingPublisher : ITickAggregationEventPublisher
    {
        public TaskCompletionSource<FuturesTickTradeDataChangedEvent> Trade { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<FuturesMarketPriceUpdatedRealtimeEvent> MarketPrice { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsRunning { get; private set; }
        public ValueTask StartAsync()
        {
            IsRunning = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event)
        {
            MarketPrice.TrySetResult(@event);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event)
        {
            Trade.TrySetResult(@event);
            return ValueTask.CompletedTask;
        }

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

    private sealed class FiniteFeed(
        InstrumentKey instrument,
        params MarketRecord64[] records) : IDatabentoTickerFeed
    {
        private readonly BoundedBatchChannel _channel = new(4, 64);
        private bool _leased;

        public void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout) { }

        public void Start(TimeSpan timeout)
        {
            var batch = _channel.RentBatch(static () => false);
            foreach (var record in records)
                batch.Add(record);
            if (!_channel.Publish(batch, static () => false))
                throw new InvalidOperationException("Unable to publish the integration-test feed batch.");
            _channel.Complete();
        }

        public void Stop(TimeSpan timeout) => _channel.Complete();
        public ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey key) => _channel;

        public IMultiplexedTickerBatchReader GetMultiplexedReader()
        {
            if (_leased)
                throw new InvalidOperationException("The integration-test feed reader is already leased.");
            _leased = true;
            return new MultiplexedTickerBatchReader(
                [(instrument, _channel)],
                () => _leased = false);
        }

        public IReadOnlyList<TickerInstrumentRegistration> GetInstruments() =>
            [new TickerInstrumentRegistration("TEST", instrument.InstrumentId.ToString(), instrument)];

        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose() => _channel.Complete();
    }

    private sealed class MemoryRedisCache : IRedisCache
    {
        private readonly Dictionary<string, string> _values = [];

        public void Set(string key, string value) => _values[key] = value;
        public void Set(string key, string value, TimeSpan expiry) => Set(key, value);
        public void Set(string key, string value, DateTimeOffset absoluteExpiry, TimeSpan ttl) => Set(key, value);
        public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
        public bool TryGet(string key, out string? value)
        {
            var found = _values.TryGetValue(key, out var stored);
            value = stored;
            return found;
        }
        public void Remove(string key) => _values.Remove(key);
        public long RemoveByPrefix(string prefix)
        {
            var keys = _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            foreach (var key in keys)
                _values.Remove(key);
            return keys.Length;
        }
        public Task SetAsync(string key, string value)
        {
            Set(key, value);
            return Task.CompletedTask;
        }
        public Task SetAsync(string key, string value, TimeSpan expiry) => SetAsync(key, value);
        public Task SetAsync(string key, string value, DateTimeOffset absoluteExpiry, TimeSpan ttl) => SetAsync(key, value);
        public Task<string?> GetAsync(string key) => Task.FromResult(Get(key));
        public long Increment(string key)
        {
            var next = long.TryParse(Get(key), out var current) ? current + 1 : 1;
            Set(key, next.ToString());
            return next;
        }
        public void DeleteAllKeys() => _values.Clear();
    }
}
