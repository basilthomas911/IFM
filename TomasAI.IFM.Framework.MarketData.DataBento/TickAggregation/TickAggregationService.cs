using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using System.Collections.Frozen;

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
    private readonly IDatabentoLastPriceWriter? _lastPrices;
    private readonly IDatabentoLastPriceReaderProvider? _lastPriceReaders;
    private readonly ITickLiveRouter? _liveRouter;
    private readonly ITickerStreamRouteController? _streamRoutes;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Dictionary<InstrumentKey, TickerState> _states = [];
    private FrozenDictionary<string, TickerState> _statesByContractId =
        FrozenDictionary<string, TickerState>.Empty;
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
        TimeProvider? timeProvider = null,
        IDatabentoLastPriceWriter? lastPrices = null,
        ITickLiveRouter? liveRouter = null,
        ITickerStreamRouteController? streamRoutes = null)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _quotePool = quotePool ?? throw new ArgumentNullException(nameof(quotePool));
        _valueDates = valueDates ?? throw new ArgumentNullException(nameof(valueDates));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(options);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastPrices = lastPrices;
        _lastPriceReaders = lastPrices as IDatabentoLastPriceReaderProvider;
        _liveRouter = liveRouter;
        _streamRoutes = streamRoutes;
    }

    public bool IsRunning => Volatile.Read(ref _running) != 0;

    public TickAggregationTickerStatus GetTickerStatus(string futuresContractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(futuresContractId);

        var states = Volatile.Read(ref _statesByContractId);
        var configured = states.ContainsKey(futuresContractId);
        var worker = Volatile.Read(ref _worker);
        var serviceRunning = IsRunning
            && Volatile.Read(ref _stopping) == 0
            && worker is { IsCompleted: false };

        return new TickAggregationTickerStatus(
            futuresContractId,
            serviceRunning,
            configured,
            serviceRunning && configured);
    }

    public TickAggregationContractStatus GetContractStatus(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        var states = Volatile.Read(ref _statesByContractId);
        var configured = states.TryGetValue(contractId, out var state);
        var worker = Volatile.Read(ref _worker);
        var serviceRunning = IsRunning
            && Volatile.Read(ref _stopping) == 0
            && worker is { IsCompleted: false };

        return new TickAggregationContractStatus(
            contractId,
            configured ? state!.Mapping.AssetTypeId : AssetTypeId.Unknown,
            serviceRunning,
            configured,
            serviceRunning && configured);
    }

    /// <summary>
    /// Reads the latest normalized market-price snapshot without consulting stream ownership.
    /// </summary>
    /// <param name="contractId">The domain contract identifier.</param>
    /// <param name="snapshot">The latest combined quote and trade snapshot when available.</param>
    /// <returns>
    /// <see langword="true"/> when a price has been observed for the contract;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        snapshot = default;
        var states = Volatile.Read(ref _statesByContractId);
        if (!states.TryGetValue(contractId, out var state))
            return false;

        return state.MarketPrice.TryRead(out snapshot);
    }

    /// <summary>Reads the latest futures-option snapshot without consulting stream ownership.</summary>
    public bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        snapshot = default;
        var states = Volatile.Read(ref _statesByContractId);
        if (!states.TryGetValue(contractId, out var state)
            || state.Mapping.AssetTypeId != AssetTypeId.FuturesOption
            || !state.MarketPrice.TryRead(out var price))
            return false;

        snapshot = new OptionTickerPriceSnapshot(
            ToTickerPriceSnapshot(price),
            TryReadOptionGreeks(price));
        return true;
    }

    /// <summary>Returns whether at least one workflow owns the contract's transient stream.</summary>
    public bool IsTickDataStreamActive(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!IsRunning || Volatile.Read(ref _stopping) != 0)
            return false;
        var states = Volatile.Read(ref _statesByContractId);
        if (!states.TryGetValue(contractId, out var state))
            return false;
        lock (state.StreamSync)
            return IsRunning
                && Volatile.Read(ref _stopping) == 0
                && state.StreamOwners.Count != 0;
    }

    /// <summary>Adds an idempotent stream owner and activates routing for the first owner.</summary>
    public bool StartTickDataStream(TickerStreamOwner owner, string contractId)
    {
        owner.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!IsRunning || Volatile.Read(ref _stopping) != 0)
            throw new InvalidOperationException("TickAggregation is not running.");
        var states = Volatile.Read(ref _statesByContractId);
        if (!states.TryGetValue(contractId, out var state))
            throw new KeyNotFoundException($"Tick contract '{contractId}' is not configured.");

        lock (state.StreamSync)
        {
            if (!IsRunning || Volatile.Read(ref _stopping) != 0)
                throw new InvalidOperationException("TickAggregation is not running.");
            if (!state.StreamOwners.Add(owner))
                return false;
            if (state.StreamOwners.Count != 1)
                return true;
            try { _streamRoutes?.Activate(state.Mapping); }
            catch
            {
                state.StreamOwners.Remove(owner);
                throw;
            }
            return true;
        }
    }

    /// <summary>Removes a stream owner and deactivates routing after the final owner leaves.</summary>
    public bool StopTickDataStream(TickerStreamOwner owner, string contractId)
    {
        owner.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        var states = Volatile.Read(ref _statesByContractId);
        if (!states.TryGetValue(contractId, out var state))
            return false;

        lock (state.StreamSync)
        {
            if (!state.StreamOwners.Remove(owner))
                return false;
            if (state.StreamOwners.Count != 0)
                return true;
            try { _streamRoutes?.Deactivate(state.Mapping); }
            catch
            {
                state.StreamOwners.Add(owner);
                throw;
            }
            return true;
        }
    }

    private OptionGreeksSnapshot? TryReadOptionGreeks(FuturesMarketPriceSnapshot price)
    {
        if (_lastPriceReaders is null)
            return null;
        var reader = _lastPriceReaders.GetFuturesOptionReader(price.ContractId, price.ValueDate);
        if (reader.TryGetLastQuoteWithGreeks(out var quoteWithGreeks)
            && price.Quote is { } quote
            && quote.SourceSequence == quoteWithGreeks.Tick.SourceSequence)
            return quoteWithGreeks.Greeks;
        if (reader.TryGetLastTradeWithGreeks(out var tradeWithGreeks)
            && price.Trade is { } trade
            && trade.SourceSequence == tradeWithGreeks.Tick.SourceSequence)
            return tradeWithGreeks.Greeks;
        return null;
    }

    private static TickerPriceSnapshot ToTickerPriceSnapshot(FuturesMarketPriceSnapshot price) => new(
        price.ContractId,
        price.InstrumentId,
        price.PublisherId,
        price.AssetTypeId,
        price.ValueDate,
        price.Quote is { } quote
            ? new TickerQuoteSnapshot(
                quote.BidPrice, quote.BidSize, quote.AskPrice, quote.AskSize,
                quote.BidCount, quote.AskCount, quote.SourceSequence,
                quote.EventTimestamp, quote.ReceiveTimestamp)
            : null,
        price.Trade is { } trade
            ? new TickerTradeSnapshot(
                trade.LastPrice, trade.LastSize, trade.SourceSequence,
                trade.EventTimestamp, trade.ReceiveTimestamp)
            : null);

    private void ClearAllStreams()
    {
        List<Exception>? failures = null;
        foreach (var state in _states.Values)
        {
            lock (state.StreamSync)
            {
                if (state.StreamOwners.Count == 0) continue;
                state.StreamOwners.Clear();
                try { _streamRoutes?.Deactivate(state.Mapping); }
                catch (Exception exception) { (failures ??= []).Add(exception); }
            }
        }
        if (failures is not null)
            throw new AggregateException("One or more ticker stream routes could not be released.", failures);
    }

    public async ValueTask StartAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsRunning) return;
            _states.Clear();
            Volatile.Write(
                ref _statesByContractId,
                FrozenDictionary<string, TickerState>.Empty);
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
                    if (mapping.AssetTypeId is not (AssetTypeId.Futures or AssetTypeId.FuturesOption))
                        throw new InvalidOperationException(
                            $"Tick aggregation accepts futures and futures-option mappings; " +
                            $"{mapping.ContractId} is {mapping.AssetTypeId}.");
                    _lastPrices?.RegisterContract(
                        mapping.ContractId,
                        mapping.AssetTypeId);
                    _states.Add(registration.Instrument, new TickerState(mapping));
                }
                Volatile.Write(
                    ref _statesByContractId,
                    _states.Values.ToFrozenDictionary(
                        state => state.Mapping.ContractId,
                        StringComparer.Ordinal));
                Volatile.Write(ref _activeTickers, _states.Count);
                _reader = _feed.GetMultiplexedReader();
                Volatile.Write(ref _running, 1);
                _worker = Task.Run(ProcessAsync);
            }
            catch
            {
                Volatile.Write(
                    ref _statesByContractId,
                    FrozenDictionary<string, TickerState>.Empty);
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
            try { ClearAllStreams(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            try { _feed.Stop(_options.FeedStopTimeout); }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
                // The feed owns its still-live producer, drain, buffers, and
                // published leases after an incomplete bounded stop. Do not wait
                // indefinitely for channel completion or reclaim those resources;
                // leave the service in Stopping so a later StopAsync can retry.
                throw new AggregateException("Tick aggregation shutdown failed.", failures);
            }
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
            state.MarketPrice.Reset();
        }

        switch (record.Header.RecordKind)
        {
            case MarketRecordKind.Quote:
                Interlocked.Increment(ref _sourceQuoteRecords);
                UpdateLastQuote(state, record.Quote);
                if (_liveRouter is not null
                    && _liveRouter.IsActive(state.Mapping.ContractId))
                    await _liveRouter.RouteAsync(CreateLiveQuote(state, record.Quote))
                        .ConfigureAwait(false);
                AddQuote(state, record.Quote);
                if (state.QuoteCount == FuturesTickQuoteDataSegment.MaximumCount)
                    await FlushAsync(state, QuoteEmissionReason.BufferFull, observedUtc).ConfigureAwait(false);
                break;
            case MarketRecordKind.Trade:
                Interlocked.Increment(ref _sourceTradeRecords);
                if (UpdateLastTrade(state, record.Trade, out var marketPrice))
                {
                    await PublishMarketPriceAsync(state, marketPrice, observedUtc)
                        .ConfigureAwait(false);
                }
                if (_liveRouter is not null
                    && _liveRouter.IsActive(state.Mapping.ContractId))
                    await _liveRouter.RouteAsync(CreateLiveTrade(state, record.Trade))
                        .ConfigureAwait(false);
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

    private void UpdateLastQuote(TickerState state, QuoteRecord64 quote)
    {
        var quoteSnapshot = new LastQuoteTickSnapshot(
            state.Mapping.ContractId,
            state.ValueDate,
            ScaleNullable(quote.BidPrice),
            quote.BidSize,
            quote.BidCount,
            ScaleNullable(quote.AskPrice),
            quote.AskSize,
            quote.AskCount,
            quote.Header.Sequence,
            FromUnixNanoseconds(quote.Header.EventTimestampNanoseconds),
            FromUnixNanoseconds(quote.Header.ReceiveTimestampNanoseconds));

        _lastPrices?.TryUpdateQuote(quoteSnapshot);
        state.MarketPrice.TryUpdateQuote(
            state.ValueDate,
            new FuturesMarketQuoteSnapshot(
                quoteSnapshot.BidPrice,
                quoteSnapshot.BidSize,
                quoteSnapshot.AskPrice,
                quoteSnapshot.AskSize,
                quoteSnapshot.BidCount,
                quoteSnapshot.AskCount,
                quoteSnapshot.SourceSequence,
                quoteSnapshot.EventTimestamp,
                quoteSnapshot.ReceiveTimestamp),
            out _);
    }

    private bool UpdateLastTrade(
        TickerState state,
        TradeRecord64 trade,
        out FuturesMarketPriceSnapshot snapshot)
    {
        var tradeSnapshot = new LastTradeTickSnapshot(
            state.Mapping.ContractId,
            state.ValueDate,
            trade.Price / PriceScale,
            trade.Size,
            trade.Header.Sequence,
            FromUnixNanoseconds(trade.Header.EventTimestampNanoseconds),
            FromUnixNanoseconds(trade.Header.ReceiveTimestampNanoseconds));

        _lastPrices?.TryUpdateTrade(tradeSnapshot);
        return state.MarketPrice.TryUpdateTrade(
            state.ValueDate,
            new FuturesMarketTradeSnapshot(
                tradeSnapshot.Price,
                tradeSnapshot.Size,
                tradeSnapshot.SourceSequence,
                tradeSnapshot.EventTimestamp,
                tradeSnapshot.ReceiveTimestamp),
            out snapshot);
    }

    private async ValueTask PublishMarketPriceAsync(
        TickerState state,
        FuturesMarketPriceSnapshot snapshot,
        DateTime observedUtc)
    {
        var entity = new TickDataEntityId(
            state.Mapping.ContractId,
            state.ValueDate,
            state.Mapping.AssetTypeId);
        var @event = new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                entity.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entity,
            AggregateId = entity.Format(),
            EventSource = nameof(TickAggregationService),
            ReceivedOn = DateTime.SpecifyKind(observedUtc, DateTimeKind.Utc),
            Price = snapshot
        };

        try
        {
            await _publisher.PublishAsync(@event).ConfigureAwait(false);
        }
        catch
        {
            // Core NATS delivery is intentionally non-durable. Preserve feed ingestion and expose
            // the missed notification through publication-failure metrics; the cache remains current.
            Interlocked.Increment(ref _publicationFailures);
        }
    }

    private static LiveTickQuoteServiceEvent CreateLiveQuote(
        TickerState state,
        QuoteRecord64 quote) => new(
        Guid.NewGuid(),
        state.Mapping.ContractId,
        state.ValueDate,
        state.Mapping.AssetTypeId,
        state.Mapping.Dataset,
        state.Mapping.DefinitionDate,
        state.Mapping.PublisherId,
        state.Mapping.InstrumentId,
        new FuturesTickQuoteData(
            quote.Header.Sequence,
            quote.Header.EventTimestampNanoseconds,
            quote.Header.ReceiveTimestampNanoseconds,
            quote.Header.Flags,
            quote.BidPrice,
            ScaleNullable(quote.BidPrice),
            quote.BidSize,
            quote.BidCount,
            quote.AskPrice,
            ScaleNullable(quote.AskPrice),
            quote.AskSize,
            quote.AskCount));

    private static LiveTickTradeServiceEvent CreateLiveTrade(
        TickerState state,
        TradeRecord64 trade) => new(
        Guid.NewGuid(),
        state.Mapping.ContractId,
        state.ValueDate,
        state.Mapping.AssetTypeId,
        state.Mapping.Dataset,
        state.Mapping.DefinitionDate,
        state.Mapping.PublisherId,
        state.Mapping.InstrumentId,
        new FuturesTickTradeData(
            trade.Header.Sequence,
            trade.Header.EventTimestampNanoseconds,
            trade.Header.ReceiveTimestampNanoseconds,
            trade.Header.Flags,
            trade.Price,
            trade.Price / PriceScale,
            trade.Size,
            trade.Action,
            trade.Side,
            trade.DbnFlags));

    private static DateTimeOffset FromUnixNanoseconds(long nanoseconds)
    {
        try
        {
            return DateTimeOffset.UnixEpoch.AddTicks(nanoseconds / 100L);
        }
        catch (ArgumentOutOfRangeException)
        {
            return nanoseconds < 0 ? DateTimeOffset.MinValue : DateTimeOffset.MaxValue;
        }
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
        public MarketPriceCache MarketPrice { get; } = new(mapping);
        public object StreamSync { get; } = new();
        public HashSet<TickerStreamOwner> StreamOwners { get; } = [];
        public DateOnly ValueDate;
        public long Sequence;
        public ITickQuoteBufferLease? QuoteLease;
        public ushort QuoteCount;
        public PendingQuotePublication? PendingQuote;
        public PendingTradePublication? PendingTrade;
        public bool HasSourceSequence;
        public uint HighestSourceSequence;
    }

    /// <summary>
    /// Provides one allocation-free writer and coherent lock-free readers for a contract snapshot.
    /// TickAggregation owns the single-writer invariant.
    /// </summary>
    private sealed class MarketPriceCache(TickContractMapping mapping)
    {
        private int _version;
        private bool _hasValue;
        private FuturesMarketPriceSnapshot _snapshot;

        public bool TryRead(out FuturesMarketPriceSnapshot snapshot)
        {
            while (true)
            {
                var before = Volatile.Read(ref _version);
                if ((before & 1) != 0)
                {
                    Thread.SpinWait(1);
                    continue;
                }

                var hasValue = _hasValue;
                var current = _snapshot;
                var after = Volatile.Read(ref _version);
                if (before != after)
                    continue;

                snapshot = current;
                return hasValue;
            }
        }

        public bool TryUpdateQuote(
            DateOnly valueDate,
            FuturesMarketQuoteSnapshot quote,
            out FuturesMarketPriceSnapshot snapshot)
        {
            var current = _snapshot;
            if (_hasValue
                && current.ValueDate == valueDate
                && current.Quote is { } existing
                && IsOlderOrEqual(
                    existing.SourceSequence,
                    existing.EventTimestamp,
                    quote.SourceSequence,
                    quote.EventTimestamp))
            {
                snapshot = current;
                return false;
            }

            snapshot = Create(
                valueDate,
                quote,
                _hasValue && current.ValueDate == valueDate
                    ? current.Trade
                    : null);
            Write(snapshot);
            return true;
        }

        public bool TryUpdateTrade(
            DateOnly valueDate,
            FuturesMarketTradeSnapshot trade,
            out FuturesMarketPriceSnapshot snapshot)
        {
            var current = _snapshot;
            if (_hasValue
                && current.ValueDate == valueDate
                && current.Trade is { } existing
                && IsOlderOrEqual(
                    existing.SourceSequence,
                    existing.EventTimestamp,
                    trade.SourceSequence,
                    trade.EventTimestamp))
            {
                snapshot = current;
                return false;
            }

            snapshot = Create(
                valueDate,
                _hasValue && current.ValueDate == valueDate
                    ? current.Quote
                    : null,
                trade);
            Write(snapshot);
            return true;
        }

        public void Reset()
        {
            var odd = Interlocked.Increment(ref _version);
            _snapshot = default;
            _hasValue = false;
            Volatile.Write(ref _version, unchecked(odd + 1));
        }

        private FuturesMarketPriceSnapshot Create(
            DateOnly valueDate,
            FuturesMarketQuoteSnapshot? quote,
            FuturesMarketTradeSnapshot? trade) => new(
            mapping.ContractId,
            mapping.InstrumentId,
            mapping.PublisherId,
            mapping.AssetTypeId,
            valueDate,
            quote,
            trade);

        private void Write(FuturesMarketPriceSnapshot snapshot)
        {
            var odd = Interlocked.Increment(ref _version);
            _snapshot = snapshot;
            _hasValue = true;
            Volatile.Write(ref _version, unchecked(odd + 1));
        }

        private static bool IsOlderOrEqual(
            long currentSequence,
            DateTimeOffset currentTimestamp,
            long candidateSequence,
            DateTimeOffset candidateTimestamp) =>
            candidateSequence < currentSequence
            || (candidateSequence == currentSequence
                && candidateTimestamp <= currentTimestamp);
    }

    private sealed record PendingQuotePublication(
        TickDataId TickDataId,
        Guid EventId,
        Guid CommandId,
        DateTime TimestampUtc,
        QuoteEmissionReason Reason);

    private sealed record PendingTradePublication(FuturesTickTradeDataChangedEvent Event);

}
