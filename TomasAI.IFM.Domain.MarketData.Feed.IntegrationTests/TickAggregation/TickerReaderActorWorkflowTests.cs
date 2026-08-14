using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
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
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using OptionTradeHandler = TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.FuturesTickTradeDataInserted;
using FuturesTradeHandler = TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.FuturesTickTradeDataInserted;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.TickAggregation;

/// <summary>
/// Exercises the real TickAggregation price/lease implementation together with the downstream domain handlers.
/// </summary>
public sealed class TickerReaderActorWorkflowTests
{
    private static readonly DateOnly ValueDate = new(2026, 8, 14);

    [Fact]
    public async Task Persisted_futures_trade_uses_active_lease_and_exact_decimal_trade()
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

        var marketDataApi = CreateMarketDataApi(aggregation);
        var logger = Substitute.For<ILogger<global::TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor.FuturesTickDataEventActor>>();
        var parameters = new FuturesTickDataEventParameters(
            marketDataApi,
            new BlackboardService(
                Substitute.For<IRedisCache>(),
                new SystemTextJsonSerializer()),
            Substitute.For<IStatusConsoleWriter>(),
            logger);
        await parameters.Readers.AcquireAsync(
            marketDataApi,
            new TickerReaderOwner("IntegrationWorkflow", "futures", "underlying"),
            contractId);
        var changed = await publisher.Trade.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var inserted = ToInserted(changed);
        var commandApi = Substitute.For<IActorMarketDataFeedCommandApi>();

        var handled = await FuturesTradeHandler.ExecuteAsync(
            inserted,
            Substitute.For<IEventActorContext>(),
            commandApi,
            parameters,
            logger);

        handled.Should().BeTrue();
        var emitted = commandApi.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IActorMarketDataFeedCommandApi.InsertVixFuturesEodDataAsync))
            .GetArguments()[0]
            .Should().BeOfType<FuturesTickDataV2ReadModel>().Which;
        emitted.ContractId.Should().Be(contractId);
        emitted.Price.Should().Be(20.15m);
        emitted.Size.Should().Be(17);
        emitted.TickId.Should().Be(2);

        await parameters.Readers.DisposeAsync();
        await aggregation.StopAsync();
    }

    [Fact]
    public async Task Persisted_option_trade_combines_exact_trade_with_aggregation_quote()
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
        var parameters = new FuturesOptionTickDataEventParameters(
            marketDataApi,
            Substitute.For<IBlackboardService>(),
            Substitute.For<IOptionTradeLiveFeedMap>(),
            Substitute.For<IStatusConsoleWriter>(),
            logger);
        await parameters.Readers.AcquireAsync(
            marketDataApi,
            new TickerReaderOwner("IntegrationWorkflow", "option", "long-call"),
            contractId);
        var changed = await publisher.Trade.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var inserted = ToInserted(changed);
        var eventApi = Substitute.For<IActorMarketDataFeedEventApi>();

        var handled = await OptionTradeHandler.ExecuteAsync(
            inserted,
            Substitute.For<IEventActorContext>(),
            eventApi,
            parameters,
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

        await parameters.Readers.DisposeAsync();
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
        leaseRoutes: new NoOpLeaseRoutes());

    private static IMarketDataApi CreateMarketDataApi(ITickerDataReaderFactory readers)
    {
        var api = Substitute.For<IMarketDataApi>();
        api.CreateTickerDataReaderAsync(
                Arg.Any<TickerReaderOwner>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => readers.CreateAsync(
                call.ArgAt<TickerReaderOwner>(0),
                call.ArgAt<string>(1),
                call.ArgAt<CancellationToken>(2)));
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
            ActorType.Event,
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

    private sealed class NoOpLeaseRoutes : ITickerLeaseRouteController
    {
        public void Activate(TickContractMapping mapping) { }
        public void Deactivate(TickContractMapping mapping) { }
    }

    private sealed class CapturingPublisher : ITickAggregationEventPublisher
    {
        public TaskCompletionSource<FuturesTickTradeDataChangedEvent> Trade { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsRunning { get; private set; }
        public ValueTask StartAsync()
        {
            IsRunning = true;
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
}
