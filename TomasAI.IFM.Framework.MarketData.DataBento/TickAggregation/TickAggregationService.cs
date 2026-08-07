using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

public sealed class TickAggregationService : ITickAggregationService, ITickAggregationMetricsSource
{
    private const decimal PriceScale = 1_000_000_000m;
    private const long UndefinedPrice = long.MaxValue;
    private readonly IDatabentoTickerFeed _feed;
    private readonly ITickContractMappingProvider _mappings;
    private readonly ITickAggregationEventPublisher _publisher;
    private readonly ITickQuoteBufferPool _quotePool;
    private readonly ITickValueDateProvider _valueDates;
    private readonly TimeProvider _timeProvider;
    private readonly TickAggregationOptions _options;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Dictionary<InstrumentKey, TickerState> _states = [];
    private IMultiplexedTickerBatchReader? _reader;
    private Task? _worker;
    private int _running;
    private int _stopping;
    private long _sourceQuoteRecords;
    private long _sourceTradeRecords;
    private long _emittedQuoteBatches;
    private long _emittedQuoteItems;
    private long _emittedTradeEvents;
    private long _bufferFullFlushes;
    private long _partialQuoteFlushes;
    private long _duplicateSourceSequences;
    private long _outOfOrderSourceSequences;
    private long _sourceSequenceGaps;
    private long _publicationFailures;
    private int _activeTickers;
    private int _outstandingQuoteBuffers;

    public TickAggregationService(
        IDatabentoTickerFeed feed,
        ITickContractMappingProvider mappings,
        ITickAggregationEventPublisher publisher,
        ITickQuoteBufferPool quotePool,
        ITickValueDateProvider valueDates,
        TickAggregationOptions options,
        TimeProvider? timeProvider = null)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _quotePool = quotePool ?? throw new ArgumentNullException(nameof(quotePool));
        _valueDates = valueDates ?? throw new ArgumentNullException(nameof(valueDates));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(options);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsRunning => Volatile.Read(ref _running) != 0;

    public async ValueTask StartAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsRunning) return;
            _states.Clear();
            Volatile.Write(ref _stopping, 0);
            await _publisher.StartAsync().ConfigureAwait(false);
            var feedStarted = false;
            try
            {
                _feed.Start(_options.FeedStartTimeout);
                feedStarted = true;
                foreach (var registration in _feed.GetInstruments())
                {
                    if (!_mappings.TryGetMapping(_options.Dataset, _options.DefinitionDate, registration.Instrument, out var mapping))
                        throw new KeyNotFoundException($"No tick mapping exists for {registration.Instrument}.");
                    if (mapping.AssetTypeId != AssetTypeId.Futures)
                        throw new InvalidOperationException($"V1 accepts only futures mappings; {mapping.ContractId} is {mapping.AssetTypeId}.");
                    _states.Add(registration.Instrument, new TickerState(mapping));
                }
                Volatile.Write(ref _activeTickers, _states.Count);
                _reader = _feed.GetMultiplexedReader();
                Volatile.Write(ref _running, 1);
                _worker = Task.Run(ProcessAsync);
            }
            catch
            {
                Volatile.Write(ref _activeTickers, 0);
                _reader?.Dispose();
                _reader = null;
                if (feedStarted)
                {
                    try { _feed.Stop(_options.FeedStopTimeout); }
                    catch { /* Preserve the original startup failure. */ }
                }
                try { await _publisher.StopAsync().ConfigureAwait(false); }
                catch { /* Preserve the original startup failure. */ }
                throw;
            }
        }
        finally { _lifecycle.Release(); }
    }

    public async ValueTask StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsRunning) return;
            Volatile.Write(ref _stopping, 1);
            List<Exception>? failures = null;
            try { _feed.Stop(_options.FeedStopTimeout); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            try { if (_worker is not null) await _worker.ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            try { await FlushAllAsync(QuoteEmissionReason.FeedStopped).ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            _reader?.Dispose();
            _reader = null;
            _worker = null;
            try { await _publisher.StopAsync().ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            Volatile.Write(ref _running, 0);
            Volatile.Write(ref _activeTickers, 0);
            if (failures is not null)
                throw new AggregateException("Tick aggregation shutdown failed.", failures);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            InstrumentBatch64 leased;
            try
            {
                leased = _reader!.Read(_options.ReaderPollTimeout);
            }
            catch (TimeoutException)
            {
                if (Volatile.Read(ref _stopping) != 0 && _reader!.IsCompleted) break;
                continue;
            }
            catch (EndOfStreamException) { break; }

            using (leased)
            {
                var state = _states[leased.Instrument];
                for (var index = 0; index < leased.Batch.Count; index++)
                {
                    var record = leased.Batch.Records[index];
                    await ProcessRecordAsync(state, record).ConfigureAwait(false);
                }
            }
        }
    }

    private async ValueTask ProcessRecordAsync(TickerState state, MarketRecord64 record)
    {
        TrackSourceSequence(state, record.Header.Sequence);
        var observedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var valueDate = _valueDates.GetValueDate(observedUtc);
        if (state.ValueDate != default && state.ValueDate != valueDate)
            await FlushAsync(state, QuoteEmissionReason.ValueDateChanged).ConfigureAwait(false);
        if (state.ValueDate != valueDate)
        {
            state.ValueDate = valueDate;
            state.Sequence = 0;
        }

        switch (record.Header.RecordKind)
        {
            case MarketRecordKind.Quote:
                Interlocked.Increment(ref _sourceQuoteRecords);
                AddQuote(state, record.Quote);
                if (state.QuoteCount == FuturesTickQuoteDataSegment.MaximumCount)
                    await FlushAsync(state, QuoteEmissionReason.BufferFull, observedUtc).ConfigureAwait(false);
                break;
            case MarketRecordKind.Trade:
                Interlocked.Increment(ref _sourceTradeRecords);
                var quotePending = state.QuoteCount > 0
                    ? EnsurePendingQuote(state, QuoteEmissionReason.TradeObserved, observedUtc)
                    : null;
                state.PendingTrade ??= CreatePendingTrade(
                    state,
                    record.Trade,
                    observedUtc,
                    checked(state.Sequence + (quotePending is null ? 1 : 2)));
                await FlushAsync(state, QuoteEmissionReason.TradeObserved, observedUtc).ConfigureAwait(false);
                await PublishPendingTradeAsync(state).ConfigureAwait(false);
                break;
        }
    }

    private void AddQuote(TickerState state, QuoteRecord64 quote)
    {
        if (state.QuoteLease is null)
        {
            state.QuoteLease = _quotePool.Rent();
            Interlocked.Increment(ref _outstandingQuoteBuffers);
        }
        state.QuoteLease.Buffer[state.QuoteCount++] = new FuturesTickQuoteData(
            quote.Header.Sequence, quote.Header.EventTimestampNanoseconds,
            quote.Header.ReceiveTimestampNanoseconds, quote.Header.Flags,
            quote.BidPrice, ScaleNullable(quote.BidPrice), quote.BidSize, quote.BidCount,
            quote.AskPrice, ScaleNullable(quote.AskPrice), quote.AskSize, quote.AskCount);
    }

    private async ValueTask FlushAsync(
        TickerState state,
        QuoteEmissionReason reason,
        DateTime? timestampUtc = null)
    {
        if (state.QuoteLease is null || state.QuoteCount == 0) return;
        var lease = state.QuoteLease;
        var count = state.QuoteCount;
        lease.SetCount(count);
        var pending = EnsurePendingQuote(state, reason, timestampUtc ?? _timeProvider.GetUtcNow().UtcDateTime);
        var entity = new TickDataEntityId(state.Mapping.ContractId, state.ValueDate, state.Mapping.AssetTypeId);
        var evt = new FuturesTickQuoteDataChangedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickQuoteDataChangedEvent.Actor, FuturesTickQuoteDataChangedEvent.Verb, entity.Format()),
            Id = pending.EventId, CommandId = pending.CommandId, EntityId = entity,
            AggregateId = entity.Format(), EventSource = nameof(TickAggregationService), ReceivedOn = pending.TimestampUtc,
            TickDataId = pending.TickDataId, AssetTypeId = state.Mapping.AssetTypeId, Dataset = state.Mapping.Dataset,
            DefinitionDate = state.Mapping.DefinitionDate, PublisherId = state.Mapping.PublisherId,
            InstrumentId = state.Mapping.InstrumentId, EmissionReason = pending.Reason, QuoteCount = count,
            QuoteData = new FuturesTickQuoteDataSegment(lease.Buffer, count)
        };
        try
        {
            await _publisher.PublishAsync(evt, lease).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Increment(ref _publicationFailures);
            throw;
        }
        state.Sequence = pending.TickDataId.SequenceId;
        state.QuoteLease = null;
        state.QuoteCount = 0;
        state.PendingQuote = null;
        Interlocked.Decrement(ref _outstandingQuoteBuffers);
        Interlocked.Increment(ref _emittedQuoteBatches);
        Interlocked.Add(ref _emittedQuoteItems, count);
        if (pending.Reason == QuoteEmissionReason.BufferFull)
            Interlocked.Increment(ref _bufferFullFlushes);
        else if (count < FuturesTickQuoteDataSegment.MaximumCount)
            Interlocked.Increment(ref _partialQuoteFlushes);
    }

    private async ValueTask PublishPendingTradeAsync(TickerState state)
    {
        if (state.PendingTrade is null) return;
        var pending = state.PendingTrade;
        try
        {
            await _publisher.PublishAsync(pending.Event).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Increment(ref _publicationFailures);
            throw;
        }
        state.Sequence = pending.Event.TickDataId.SequenceId;
        state.PendingTrade = null;
        Interlocked.Increment(ref _emittedTradeEvents);
    }

    private static TickDataId CreateNextId(TickerState state, DateTime timestamp) =>
        new(state.Mapping.ContractId, state.ValueDate, checked(state.Sequence + 1), DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

    private static PendingQuotePublication EnsurePendingQuote(
        TickerState state,
        QuoteEmissionReason reason,
        DateTime timestampUtc)
    {
        return state.PendingQuote ??= new PendingQuotePublication(
            CreateNextId(state, timestampUtc),
            Guid.NewGuid(),
            Guid.NewGuid(),
            timestampUtc,
            reason);
    }

    private static PendingTradePublication CreatePendingTrade(
        TickerState state,
        TradeRecord64 trade,
        DateTime timestampUtc,
        long sequence)
    {
        var entity = new TickDataEntityId(state.Mapping.ContractId, state.ValueDate, state.Mapping.AssetTypeId);
        return new PendingTradePublication(new FuturesTickTradeDataChangedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickTradeDataChangedEvent.Actor,
                FuturesTickTradeDataChangedEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entity,
            AggregateId = entity.Format(), EventSource = nameof(TickAggregationService), ReceivedOn = timestampUtc,
            TickDataId = new TickDataId(state.Mapping.ContractId, state.ValueDate, sequence,
                DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc)),
            AssetTypeId = state.Mapping.AssetTypeId, Dataset = state.Mapping.Dataset,
            DefinitionDate = state.Mapping.DefinitionDate, PublisherId = state.Mapping.PublisherId,
            InstrumentId = state.Mapping.InstrumentId,
            TradeData = new FuturesTickTradeData(
                trade.Header.Sequence, trade.Header.EventTimestampNanoseconds,
                trade.Header.ReceiveTimestampNanoseconds, trade.Header.Flags,
                trade.Price, trade.Price / PriceScale, trade.Size, trade.Action, trade.Side, trade.DbnFlags)
        });
    }

    private async ValueTask FlushAllAsync(QuoteEmissionReason reason)
    {
        foreach (var state in _states.Values)
        {
            await FlushAsync(state, reason).ConfigureAwait(false);
            await PublishPendingTradeAsync(state).ConfigureAwait(false);
        }
    }

    private static decimal? ScaleNullable(long value) => value == UndefinedPrice ? null : value / PriceScale;

    private void TrackSourceSequence(TickerState state, uint sequence)
    {
        if (!state.HasSourceSequence)
        {
            state.HasSourceSequence = true;
            state.HighestSourceSequence = sequence;
            return;
        }
        if (sequence == state.HighestSourceSequence)
        {
            Interlocked.Increment(ref _duplicateSourceSequences);
            return;
        }
        if (sequence < state.HighestSourceSequence)
        {
            Interlocked.Increment(ref _outOfOrderSourceSequences);
            return;
        }
        if (sequence > state.HighestSourceSequence + 1L)
            Interlocked.Add(ref _sourceSequenceGaps, sequence - state.HighestSourceSequence - 1L);
        state.HighestSourceSequence = sequence;
    }

    public TickAggregationMetricsSnapshot GetMetrics() => new(
        Interlocked.Read(ref _sourceQuoteRecords),
        Interlocked.Read(ref _sourceTradeRecords),
        Interlocked.Read(ref _emittedQuoteBatches),
        Interlocked.Read(ref _emittedQuoteItems),
        Interlocked.Read(ref _emittedTradeEvents),
        Interlocked.Read(ref _bufferFullFlushes),
        Interlocked.Read(ref _partialQuoteFlushes),
        Interlocked.Read(ref _duplicateSourceSequences),
        Interlocked.Read(ref _outOfOrderSourceSequences),
        Interlocked.Read(ref _sourceSequenceGaps),
        Interlocked.Read(ref _publicationFailures),
        Volatile.Read(ref _activeTickers),
        Volatile.Read(ref _outstandingQuoteBuffers));

    private static void ValidateOptions(TickAggregationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Dataset);
        if (options.DefinitionDate == default)
            throw new ArgumentOutOfRangeException(nameof(options.DefinitionDate));
        ValidateTimeout(options.FeedStartTimeout, nameof(options.FeedStartTimeout));
        ValidateTimeout(options.FeedStopTimeout, nameof(options.FeedStopTimeout));
        ValidateTimeout(options.ReaderPollTimeout, nameof(options.ReaderPollTimeout));
    }

    private static void ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        foreach (var state in _states.Values)
        {
            if (state.QuoteLease is null) continue;
            state.QuoteLease.Dispose();
            state.QuoteLease = null;
            Interlocked.Decrement(ref _outstandingQuoteBuffers);
        }
        _reader?.Dispose();
        _feed.Dispose();
        _lifecycle.Dispose();
    }

    private sealed class TickerState(TickContractMapping mapping)
    {
        public TickContractMapping Mapping { get; } = mapping;
        public DateOnly ValueDate;
        public long Sequence;
        public ITickQuoteBufferLease? QuoteLease;
        public ushort QuoteCount;
        public PendingQuotePublication? PendingQuote;
        public PendingTradePublication? PendingTrade;
        public bool HasSourceSequence;
        public uint HighestSourceSequence;
    }

    private sealed record PendingQuotePublication(
        TickDataId TickDataId,
        Guid EventId,
        Guid CommandId,
        DateTime TimestampUtc,
        QuoteEmissionReason Reason);

    private sealed record PendingTradePublication(FuturesTickTradeDataChangedEvent Event);
}
