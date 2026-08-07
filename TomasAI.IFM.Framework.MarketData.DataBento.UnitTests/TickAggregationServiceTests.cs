using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class TickAggregationServiceTests
{
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

    private sealed class MappingProvider(InstrumentKey instrument) : ITickContractMappingProvider
    {
        public bool TryGetMapping(string dataset, DateOnly definitionDate, InstrumentKey key, out TickContractMapping mapping)
        {
            mapping = new TickContractMapping(dataset, definitionDate, key.PublisherId, key.InstrumentId, "ESU6", AssetTypeId.Futures);
            return key == instrument;
        }
    }

    private sealed class CapturingPublisher : ITickAggregationEventPublisher
    {
        public List<string> Order { get; } = [];
        public List<long> Sequences { get; } = [];
        public ushort QuoteCount { get; private set; }
        public decimal? SecondBid { get; private set; }
        public bool IsRunning { get; private set; }
        public ValueTask StartAsync() { IsRunning = true; return ValueTask.CompletedTask; }
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

    private sealed class RejectFirstQuotePublisher : ITickAggregationEventPublisher
    {
        public List<(Guid EventId, Guid CommandId, long Sequence)> QuoteAttempts { get; } = [];
        public List<QuoteEmissionReason> Reasons { get; } = [];
        public bool IsRunning { get; private set; }
        public ValueTask StartAsync() { IsRunning = true; return ValueTask.CompletedTask; }
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
}
