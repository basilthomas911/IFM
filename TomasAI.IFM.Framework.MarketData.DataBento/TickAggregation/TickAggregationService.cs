using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using System.Collections.Frozen;
using System.Diagnostics;

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
    private readonly Action<string>? _terminalFaultHandler;
    private readonly ILogger<TickAggregationService> _logger;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly CancellationTokenSource _generationStopping = new();
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
    private long _processingFailures;
    private long _recordsStarted;
    private long _recordsCompleted;
    private long _sourceMboRecords;
    private long _sourceStatisticsRecords;
    private long _statisticsReplayCompleteRecords;
    private long _tradeReplayCompleteRecords;
    private long _unsupportedRecords;
    private long _totalProcessingDurationTicks;
    private long _maximumProcessingDurationTicks;
    private long _lastRecordStartedUtcTicks;
    private long _lastRecordCompletedUtcTicks;
    private long _lastRecordFailedUtcTicks;
    private long _inFlightStartedTimestamp;
    private int _currentProcessingStage;
    private TickAggregationRecordProgress? _inFlightRecord;
    private TickAggregationProcessingFailure? _lastProcessingFailure;
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
        ITickerStreamRouteController? streamRoutes = null,
        Action<string>? terminalFaultHandler = null,
        ILogger<TickAggregationService>? logger = null,
        Guid generationId = default)
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
        _terminalFaultHandler = terminalFaultHandler;
        _logger = logger ?? NullLogger<TickAggregationService>.Instance;
        GenerationId = generationId == Guid.Empty ? Guid.NewGuid() : generationId;
    }

    public bool IsRunning => Volatile.Read(ref _running) != 0;
    public Guid GenerationId { get; }

    /// <summary>
    /// Returns the native transport and managed-drain health snapshot, including terminal
    /// status and warning details when the dataset reader has completed.
    /// </summary>
    public FeedHealthSnapshot GetFeedHealth() => _feed.GetHealth();

    /// <summary>
    /// Synchronously reports whether the managed reader and its native Databento transport are
    /// both running. Expected lifecycle and interop failures are represented as
    /// <see langword="false"/>.
    /// </summary>
    public bool IsFeedUp()
    {
        try
        {
            var worker = Volatile.Read(ref _worker);
            if (!IsRunning
                || Volatile.Read(ref _stopping) != 0
                || worker is not { IsCompleted: false })
                return false;

            var health = _feed.GetHealth();
            return health.TransportReady
                && health.State == FeedState.Running
                && health.TerminalStatus == DatabentoFeedStatus.Ok;
        }
        catch
        {
            return false;
        }
    }

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

        var streamActive = false;
        if (state is not null)
        {
            lock (state.StreamSync)
                streamActive = state.StreamOwners.Count != 0;
        }

        return new TickAggregationContractStatus(
            contractId,
            configured ? state!.Mapping.AssetTypeId : AssetTypeId.Unknown,
            serviceRunning,
            configured,
            serviceRunning && configured,
            serviceRunning && streamActive,
            ReadTimestamp(configured
                ? Interlocked.Read(ref state!.LastSourceRecordObservedUtcTicks)
                : 0),
            ReadTimestamp(configured
                ? Interlocked.Read(ref state!.LastMarketPricePublishedUtcTicks)
                : 0),
            ReadTimestamp(configured
                ? Interlocked.Read(ref state!.LastDurableTickPublishedUtcTicks)
                : 0),
            ReadTimestamp(configured
                ? Interlocked.Read(ref state!.StreamActivatedUtcTicks)
                : 0),
            ReadTimestamp(configured
                ? Interlocked.Read(ref state!.LastAcceptedCacheUpdateUtcTicks)
                : 0),
            ReadTimestamp(configured
                ? Interlocked.Read(ref state!.LastAcceptedSourceEventUtcTicks)
                : 0),
            configured ? Interlocked.Read(ref state!.AcceptedCacheUpdates) : 0,
            configured ? Interlocked.Read(ref state!.RejectedCacheUpdates) : 0);
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

    /// <summary>Reads the latest complete session statistics without consulting stream ownership.</summary>
    public bool TryGetFuturesSessionStatistics(
        string contractId,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        snapshot = default;
        var states = Volatile.Read(ref _statesByContractId);
        return states.TryGetValue(contractId, out var state)
            && state.SessionStatistics.TryRead(
                state.Mapping.ContractId,
                state.ValueDate,
                out snapshot);
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
            try
            {
                _streamRoutes?.Activate(state.Mapping);
                Interlocked.Exchange(
                    ref state.StreamActivatedUtcTicks,
                    _timeProvider.GetUtcNow().UtcTicks);
            }
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
            Interlocked.Exchange(ref state.StreamActivatedUtcTicks, 0);
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

    public IReadOnlyDictionary<string, TickerStreamOwner[]> CaptureStreamOwners()
    {
        var result = new Dictionary<string, TickerStreamOwner[]>(StringComparer.Ordinal);
        foreach (var state in Volatile.Read(ref _statesByContractId).Values)
        {
            lock (state.StreamSync)
            {
                if (state.StreamOwners.Count != 0)
                    result[state.Mapping.ContractId] = [.. state.StreamOwners];
            }
        }
        return result;
    }

    public void RestoreStreamOwners(
        IReadOnlyDictionary<string, TickerStreamOwner[]> ownersByContract)
    {
        ArgumentNullException.ThrowIfNull(ownersByContract);
        foreach (var pair in ownersByContract)
        {
            foreach (var owner in pair.Value)
                StartTickDataStream(owner, pair.Key);
        }
    }

    public ValueTask StartAsync() => StartAsync(CancellationToken.None);

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning) return;
            _states.Clear();
            Volatile.Write(
                ref _statesByContractId,
                FrozenDictionary<string, TickerState>.Empty);
            Volatile.Write(ref _stopping, 0);
            await _publisher.StartAsync(cancellationToken).ConfigureAwait(false);
            var feedStarted = false;
            try
            {
                using var consumerReady = new ManualResetEventSlim(false);
                _feed.Start(_options.FeedStartTimeout, remaining =>
                {
                    foreach (var registration in _feed.GetInstruments())
                    {
                        if (!_mappings.TryResolveFeedMapping(
                                _options.Dataset,
                                _options.DefinitionDate,
                                registration,
                                out var mapping))
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
                    _worker = Task.Run(
                        () => ProcessAsync(_generationStopping.Token, consumerReady),
                        CancellationToken.None);
                    if (!consumerReady.Wait(remaining))
                        throw new TimeoutException(
                            "The tick aggregation consumer did not become ready before feed activation.");
                });
                feedStarted = true;
            }
            catch
            {
                await _generationStopping.CancelAsync().ConfigureAwait(false);
                Volatile.Write(
                    ref _statesByContractId,
                    FrozenDictionary<string, TickerState>.Empty);
                Volatile.Write(ref _activeTickers, 0);
                Volatile.Write(ref _running, 0);
                if (_worker is not null)
                {
                    try { await _worker.ConfigureAwait(false); }
                    catch { /* Preserve the original startup failure. */ }
                    _worker = null;
                }
                _reader?.Dispose();
                _reader = null;
                if (feedStarted)
                {
                    try { _feed.Stop(_options.FeedStopTimeout); }
                    catch { /* Preserve the original startup failure. */ }
                }
                try { await _publisher.StopAsync(cancellationToken).ConfigureAwait(false); }
                catch { /* Preserve the original startup failure. */ }
                throw;
            }
        }
        finally { _lifecycle.Release(); }
    }

    public ValueTask StopAsync() => StopAsyncCore(CancellationToken.None, fenceGeneration: false);

    public ValueTask StopAsync(CancellationToken cancellationToken) =>
        StopAsyncCore(cancellationToken, fenceGeneration: true);

    private async ValueTask StopAsyncCore(
        CancellationToken cancellationToken,
        bool fenceGeneration)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRunning) return;
            Volatile.Write(ref _stopping, 1);
            if (fenceGeneration)
                await _generationStopping.CancelAsync().ConfigureAwait(false);
            List<Exception>? failures = null;
            try { ClearAllStreams(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            try { _feed.Stop(_options.FeedStopTimeout, cancellationToken); }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
                // The feed owns its still-live producer, drain, buffers, and
                // published leases after an incomplete bounded stop. Do not wait
                // indefinitely for channel completion or reclaim those resources;
                // leave the service in Stopping so a later StopAsync can retry.
                throw new AggregateException("Tick aggregation shutdown failed.", failures);
            }
            try
            {
                if (_worker is not null)
                    await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            _reader?.Dispose();
            _reader = null;
            _worker = null;
            try { await _publisher.StopAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            Volatile.Write(ref _running, 0);
            Volatile.Write(ref _activeTickers, 0);
            if (failures is not null)
                throw new AggregateException("Tick aggregation shutdown failed.", failures);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task ProcessAsync(
        CancellationToken cancellationToken,
        ManualResetEventSlim? startupReady = null)
    {
        startupReady?.Set();
        try
        {
            while (true)
            {
                if (!_reader!.TryRead(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken,
                        out var leased))
                {
                    if (_reader.IsCompleted) break;
                    continue;
                }

                using (leased)
                {
                    var state = _states[leased.Instrument];
                    for (var index = 0; index < leased.Batch.Count; index++)
                    {
                        var record = leased.Batch.Records[index];
                        try
                        {
                            await ProcessRecordAsync(state, record, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch
                        {
                            // ProcessRecordAsync records and logs the complete failure context. A
                            // recoverable record failure must not terminate the dataset worker.
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Dataset generation was fenced and asked to quiesce.
        }
        finally
        {
            if (Volatile.Read(ref _stopping) == 0)
            {
                try { _terminalFaultHandler?.Invoke("Aggregation reader completed unexpectedly."); }
                catch { /* Terminal notification must never fault the reader task. */ }
            }
        }
    }

    private async ValueTask ProcessRecordAsync(
        TickerState state,
        MarketRecord64 record,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = _timeProvider.GetUtcNow();
        var startedTimestamp = Stopwatch.GetTimestamp();
        var header = record.Header;
        var progress = new TickAggregationRecordProgress(
            _options.Dataset,
            state.Mapping.ContractId,
            header.RecordKind.ToString(),
            header.PublisherId,
            header.InstrumentId,
            header.Sequence,
            startedAtUtc);

        Interlocked.Increment(ref _recordsStarted);
        Interlocked.Exchange(ref _lastRecordStartedUtcTicks, startedAtUtc.UtcTicks);
        TrackRecordKind(header.RecordKind);
        Interlocked.Exchange(ref _inFlightStartedTimestamp, startedTimestamp);
        Volatile.Write(ref _inFlightRecord, progress);
        Volatile.Write(ref _currentProcessingStage, (int)TickAggregationProcessingStage.Starting);

        try
        {
            await ProcessRecordCoreAsync(state, record, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _recordsCompleted);
            Interlocked.Exchange(ref _lastRecordCompletedUtcTicks, _timeProvider.GetUtcNow().UtcTicks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedAtUtc = _timeProvider.GetUtcNow();
            var elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
            var stage = (TickAggregationProcessingStage)Volatile.Read(ref _currentProcessingStage);
            Interlocked.Increment(ref _processingFailures);
            Interlocked.Exchange(ref _lastRecordFailedUtcTicks, failedAtUtc.UtcTicks);
            Volatile.Write(ref _lastProcessingFailure, new TickAggregationProcessingFailure(
                progress.Dataset,
                progress.ContractId,
                progress.RecordKind,
                progress.PublisherId,
                progress.InstrumentId,
                progress.SourceSequence,
                stage,
                failedAtUtc,
                elapsed,
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message));

            var metrics = GetMetrics();
            _logger.LogError(
                exception,
                "Tick aggregation failed processing dataset {Dataset}, contract {ContractId}, " +
                "record {RecordKind}, publisher {PublisherId}, instrument {InstrumentId}, " +
                "sequence {SourceSequence}, stage {ProcessingStage}, elapsed {ElapsedMilliseconds} ms. " +
                "Records started {RecordsStarted}, completed {RecordsCompleted}, failed {ProcessingFailures}; " +
                "quotes {SourceQuoteRecords}, trades {SourceTradeRecords}, MBO {SourceMboRecords}, " +
                "statistics {SourceStatisticsRecords}; publication failures {PublicationFailures}.",
                progress.Dataset,
                progress.ContractId,
                progress.RecordKind,
                progress.PublisherId,
                progress.InstrumentId,
                progress.SourceSequence,
                stage,
                elapsed.TotalMilliseconds,
                metrics.RecordsStarted,
                metrics.RecordsCompleted,
                metrics.ProcessingFailures,
                metrics.SourceQuoteRecords,
                metrics.SourceTradeRecords,
                metrics.SourceMboRecords,
                metrics.SourceStatisticsRecords,
                metrics.PublicationFailures);
            throw;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetElapsedTime(startedTimestamp).Ticks;
            Interlocked.Add(ref _totalProcessingDurationTicks, elapsedTicks);
            UpdateMaximum(ref _maximumProcessingDurationTicks, elapsedTicks);
            Volatile.Write(ref _currentProcessingStage, (int)TickAggregationProcessingStage.Idle);
            Volatile.Write(ref _inFlightRecord, null);
            Interlocked.Exchange(ref _inFlightStartedTimestamp, 0);
        }
    }

    private async ValueTask ProcessRecordCoreAsync(
        TickerState state,
        MarketRecord64 record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var valueDate = _valueDates.GetValueDate(observedUtc);
        if (state.ValueDate != default && state.ValueDate != valueDate)
        {
            SetProcessingStage(TickAggregationProcessingStage.ValueDateFlush);
            await FlushAsync(
                    state,
                    QuoteEmissionReason.ValueDateChanged,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        if (state.ValueDate != valueDate)
        {
            state.ValueDate = valueDate;
            state.Sequence = 0;
            state.TradeOrdinal = 0;
            state.MarketPrice.Reset();
            state.SessionStatistics.Reset();
        }

        if (record.Header.RecordKind == MarketRecordKind.StatisticsReplayComplete)
        {
            SetProcessingStage(TickAggregationProcessingStage.StatisticsReplayPublish);
            foreach (var replayedStatistics in state.SessionStatistics.ReadAll(
                         state.Mapping.ContractId))
                await PublishSessionStatisticsAsync(state, replayedStatistics, cancellationToken)
                    .ConfigureAwait(false);
            return;
        }
        if (record.Header.RecordKind == MarketRecordKind.TradeReplayComplete)
        {
            var reconstructed = state.SessionStatistics.CompleteTradeReplay(
                state.Mapping.ContractId,
                state.ValueDate);
            state.StreamEpochId = Guid.NewGuid();
            state.TradeOrdinal = 0;
            SetProcessingStage(TickAggregationProcessingStage.TradeReplayPublish);
            await PublishSessionStatisticsAsync(state, reconstructed, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        TrackSourceSequence(
            state,
            record.Header.PublisherId,
            record.Header.Sequence);

        switch (record.Header.RecordKind)
        {
            case MarketRecordKind.Quote:
                SetProcessingStage(TickAggregationProcessingStage.QuoteUpdate);
                MarkObserved(state);
                if (!UpdateLastQuote(state, record.Quote, out var quoteMarketPrice))
                {
                    Interlocked.Increment(ref state.RejectedCacheUpdates);
                    break;
                }
                MarkAccepted(state, record.Quote.Header.EventTimestampNanoseconds);
                if (IsVxFutures(state.Mapping))
                {
                    SetProcessingStage(TickAggregationProcessingStage.QuoteMarketPricePublish);
                    await PublishMarketPriceAsync(
                            state,
                            quoteMarketPrice,
                            FuturesMarketPriceUpdateSource.Quote,
                            observedUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                if (_liveRouter is not null
                    && _liveRouter.IsActive(state.Mapping.ContractId))
                {
                    SetProcessingStage(TickAggregationProcessingStage.QuoteLiveRoute);
                    await _liveRouter.RouteAsync(
                            CreateLiveQuote(state, record.Quote), cancellationToken)
                        .ConfigureAwait(false);
                }
                AddQuote(state, record.Quote);
                if (state.QuoteCount == FuturesTickQuoteDataSegment.MaximumCount)
                {
                    SetProcessingStage(TickAggregationProcessingStage.QuoteFlush);
                    await FlushAsync(
                            state,
                            QuoteEmissionReason.BufferFull,
                            observedUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                break;
            case MarketRecordKind.Trade:
                SetProcessingStage(TickAggregationProcessingStage.TradeUpdate);
                var isReplay = (record.Header.Flags & 2) != 0;
                _ = state.SessionStatistics.TryAccumulateTrade(
                    state.Mapping.ContractId,
                    state.ValueDate,
                    record.Trade,
                    isReplay,
                    out _);
                if (isReplay)
                    break;
                MarkObserved(state);
                if (!UpdateLastTrade(state, record.Trade, out var marketPrice))
                {
                    Interlocked.Increment(ref state.RejectedCacheUpdates);
                    break;
                }
                MarkAccepted(state, record.Trade.Header.EventTimestampNanoseconds);
                SetProcessingStage(TickAggregationProcessingStage.TradeMarketPricePublish);
                await PublishMarketPriceAsync(
                        state,
                        marketPrice,
                        FuturesMarketPriceUpdateSource.Trade,
                        observedUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (_liveRouter is not null
                    && _liveRouter.IsActive(state.Mapping.ContractId))
                {
                    SetProcessingStage(TickAggregationProcessingStage.TradeLiveRoute);
                    await _liveRouter.RouteAsync(
                            CreateLiveTrade(state, record.Trade), cancellationToken)
                        .ConfigureAwait(false);
                }
                var quotePending = state.QuoteCount > 0
                    ? EnsurePendingQuote(state, QuoteEmissionReason.TradeObserved, observedUtc)
                    : null;
                state.PendingTrade ??= CreatePendingTrade(
                    state,
                    record.Trade,
                    observedUtc,
                    checked(state.Sequence + (quotePending is null ? 1 : 2)));
                SetProcessingStage(TickAggregationProcessingStage.TradeQuoteFlush);
                await FlushAsync(
                        state,
                        QuoteEmissionReason.TradeObserved,
                        observedUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
                SetProcessingStage(TickAggregationProcessingStage.TradePublish);
                await PublishPendingTradeAsync(state, cancellationToken).ConfigureAwait(false);
                break;
            case MarketRecordKind.Statistics:
                SetProcessingStage(TickAggregationProcessingStage.StatisticsUpdate);
                if (state.Mapping.AssetTypeId == AssetTypeId.Futures
                    && state.SessionStatistics.TryApplyStatistic(
                        state.Mapping.ContractId,
                        state.ValueDate,
                        record.Statistics,
                        out var statistics)
                    && (record.Header.Flags & 2) == 0)
                {
                    SetProcessingStage(TickAggregationProcessingStage.StatisticsPublish);
                    await PublishSessionStatisticsAsync(state, statistics, cancellationToken)
                        .ConfigureAwait(false);
                }
                break;
        }
    }

    private async ValueTask PublishSessionStatisticsAsync(
        TickerState state,
        FuturesSessionStatisticsSnapshot statistics,
        CancellationToken cancellationToken)
    {
        var entityId = new FuturesEodDataId(
            state.Mapping.ContractId,
            statistics.ValueDate);
        var @event = new FuturesSessionStatisticsUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = nameof(TickAggregationService),
            ReceivedOn = _timeProvider.GetUtcNow().UtcDateTime,
            Statistics = statistics
        };

        try
        {
            await _publisher.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            TrackHandledPublicationException(
                state,
                exception,
                nameof(FuturesSessionStatisticsUpdatedRealtimeEvent));
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

    private bool UpdateLastQuote(
        TickerState state,
        QuoteRecord64 quote,
        out FuturesMarketPriceSnapshot snapshot)
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

        if (_lastPrices is not null && !_lastPrices.TryUpdateQuote(quoteSnapshot))
        {
            snapshot = default;
            return false;
        }
        return state.MarketPrice.TryUpdateQuote(
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
            out snapshot);
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

        if (_lastPrices is not null && !_lastPrices.TryUpdateTrade(tradeSnapshot))
        {
            snapshot = default;
            return false;
        }
        var nextTradeOrdinal = checked(state.TradeOrdinal + 1);
        var accepted = state.MarketPrice.TryUpdateTrade(
            state.ValueDate,
            new FuturesMarketTradeSnapshot(
                tradeSnapshot.Price,
                tradeSnapshot.Size,
                tradeSnapshot.SourceSequence,
                tradeSnapshot.EventTimestamp,
                tradeSnapshot.ReceiveTimestamp,
                DatabentoTradeNormalizer.MapAction(trade.Action),
                DatabentoTradeNormalizer.MapSide(trade.Side),
                DatabentoTradeNormalizer.MapConditions(trade.Header.Flags, trade.DbnFlags),
                state.StreamEpochId,
                nextTradeOrdinal),
            out snapshot);
        if (accepted)
            state.TradeOrdinal = nextTradeOrdinal;
        return accepted;
    }

    private async ValueTask PublishMarketPriceAsync(
        TickerState state,
        FuturesMarketPriceSnapshot snapshot,
        FuturesMarketPriceUpdateSource updateSource,
        DateTime observedUtc,
        CancellationToken cancellationToken)
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
            Price = snapshot,
            UpdateSource = updateSource
        };

        try
        {
            await _publisher.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
            MarkPublished(ref state.LastMarketPricePublishedUtcTicks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Core NATS delivery is intentionally non-durable. Preserve feed ingestion and expose
            // the missed notification through publication-failure metrics; the cache remains current.
            TrackHandledPublicationException(
                state,
                exception,
                nameof(FuturesMarketPriceUpdatedRealtimeEvent));
        }
    }

    private static bool IsVxFutures(TickContractMapping mapping) =>
        mapping.AssetTypeId == AssetTypeId.Futures
        && StringComparer.Ordinal.Equals(mapping.ContractDetails?.Ticker, "VX");

    private static LiveTickQuoteServiceEvent CreateLiveQuote(
        TickerState state,
        QuoteRecord64 quote) => new(
        Guid.NewGuid(),
        state.Mapping.ContractId,
        state.ValueDate,
        state.Mapping.AssetTypeId,
        state.Mapping.Dataset,
        state.Mapping.DefinitionDate,
        quote.Header.PublisherId,
        quote.Header.InstrumentId,
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
        trade.Header.PublisherId,
        trade.Header.InstrumentId,
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
        DateTime? timestampUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (state.QuoteLease is null || state.QuoteCount == 0) return;
        var lease = state.QuoteLease;
        var count = state.QuoteCount;
        lease.SetCount(count);
        var pending = EnsurePendingQuote(state, reason, timestampUtc ?? _timeProvider.GetUtcNow().UtcDateTime);
        var entity = new TickDataEntityId(state.Mapping.ContractId, state.ValueDate, state.Mapping.AssetTypeId);
        var evt = new FuturesTickQuoteDataChangedEvent
        {
            Subject = new ActorSubject(ActorType.Realtime, FuturesTickQuoteDataChangedEvent.Actor, FuturesTickQuoteDataChangedEvent.Verb, entity.Format()),
            Id = pending.EventId, CommandId = pending.CommandId, EntityId = entity,
            AggregateId = entity.Format(), EventSource = nameof(TickAggregationService), ReceivedOn = pending.TimestampUtc,
            TickDataId = pending.TickDataId, AssetTypeId = state.Mapping.AssetTypeId, Dataset = state.Mapping.Dataset,
            DefinitionDate = state.Mapping.DefinitionDate, PublisherId = state.Mapping.PublisherId,
            InstrumentId = state.Mapping.InstrumentId, EmissionReason = pending.Reason, QuoteCount = count,
            QuoteData = new FuturesTickQuoteDataSegment(lease.Buffer, count)
        };
        try
        {
            await _publisher.PublishAsync(evt, lease, cancellationToken).ConfigureAwait(false);
            MarkPublished(ref state.LastDurableTickPublishedUtcTicks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

    private async ValueTask PublishPendingTradeAsync(
        TickerState state,
        CancellationToken cancellationToken = default)
    {
        if (state.PendingTrade is null) return;
        var pending = state.PendingTrade;
        try
        {
            await _publisher.PublishAsync(pending.Event, cancellationToken).ConfigureAwait(false);
            MarkPublished(ref state.LastDurableTickPublishedUtcTicks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
            Subject = new ActorSubject(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor,
                FuturesTickTradeDataChangedEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entity,
            AggregateId = entity.Format(), EventSource = nameof(TickAggregationService), ReceivedOn = timestampUtc,
            TickDataId = new TickDataId(state.Mapping.ContractId, state.ValueDate, sequence,
                DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc)),
            AssetTypeId = state.Mapping.AssetTypeId, Dataset = state.Mapping.Dataset,
            DefinitionDate = state.Mapping.DefinitionDate, PublisherId = trade.Header.PublisherId,
            InstrumentId = trade.Header.InstrumentId,
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

    private void TrackSourceSequence(
        TickerState state,
        ushort publisherId,
        uint sequence)
    {
        if (!state.HighestSourceSequenceByPublisher.TryGetValue(
                publisherId,
                out var highestSequence))
        {
            state.HighestSourceSequenceByPublisher.Add(publisherId, sequence);
            return;
        }
        if (sequence == highestSequence)
        {
            Interlocked.Increment(ref _duplicateSourceSequences);
            return;
        }
        if (sequence < highestSequence)
        {
            Interlocked.Increment(ref _outOfOrderSourceSequences);
            return;
        }
        if (sequence > highestSequence + 1L)
            Interlocked.Add(ref _sourceSequenceGaps, sequence - highestSequence - 1L);
        state.HighestSourceSequenceByPublisher[publisherId] = sequence;
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
        Interlocked.Read(ref _processingFailures),
        Volatile.Read(ref _activeTickers),
        Volatile.Read(ref _outstandingQuoteBuffers))
    {
        RecordsStarted = Interlocked.Read(ref _recordsStarted),
        RecordsCompleted = Interlocked.Read(ref _recordsCompleted),
        SourceMboRecords = Interlocked.Read(ref _sourceMboRecords),
        SourceStatisticsRecords = Interlocked.Read(ref _sourceStatisticsRecords),
        StatisticsReplayCompleteRecords = Interlocked.Read(ref _statisticsReplayCompleteRecords),
        TradeReplayCompleteRecords = Interlocked.Read(ref _tradeReplayCompleteRecords),
        UnsupportedRecords = Interlocked.Read(ref _unsupportedRecords),
        CurrentProcessingDurationTicks = ReadCurrentProcessingDurationTicks(),
        TotalProcessingDurationTicks = Interlocked.Read(ref _totalProcessingDurationTicks),
        MaximumProcessingDurationTicks = Interlocked.Read(ref _maximumProcessingDurationTicks),
        LastRecordStartedAtUtc = ReadTimestamp(Interlocked.Read(ref _lastRecordStartedUtcTicks)),
        LastRecordCompletedAtUtc = ReadTimestamp(Interlocked.Read(ref _lastRecordCompletedUtcTicks)),
        LastRecordFailedAtUtc = ReadTimestamp(Interlocked.Read(ref _lastRecordFailedUtcTicks)),
        CurrentStage = (TickAggregationProcessingStage)Volatile.Read(ref _currentProcessingStage),
        InFlightRecord = Volatile.Read(ref _inFlightRecord),
        LastFailure = Volatile.Read(ref _lastProcessingFailure)
    };

    private long ReadCurrentProcessingDurationTicks()
    {
        var startedTimestamp = Interlocked.Read(ref _inFlightStartedTimestamp);
        return startedTimestamp == 0
            ? 0
            : Stopwatch.GetElapsedTime(startedTimestamp).Ticks;
    }

    private void TrackRecordKind(MarketRecordKind kind)
    {
        switch (kind)
        {
            case MarketRecordKind.Quote:
                Interlocked.Increment(ref _sourceQuoteRecords);
                break;
            case MarketRecordKind.Trade:
                Interlocked.Increment(ref _sourceTradeRecords);
                break;
            case MarketRecordKind.Mbo:
                Interlocked.Increment(ref _sourceMboRecords);
                break;
            case MarketRecordKind.Statistics:
                Interlocked.Increment(ref _sourceStatisticsRecords);
                break;
            case MarketRecordKind.StatisticsReplayComplete:
                Interlocked.Increment(ref _statisticsReplayCompleteRecords);
                break;
            case MarketRecordKind.TradeReplayComplete:
                Interlocked.Increment(ref _tradeReplayCompleteRecords);
                break;
            default:
                Interlocked.Increment(ref _unsupportedRecords);
                break;
        }
    }

    private void SetProcessingStage(TickAggregationProcessingStage stage) =>
        Volatile.Write(ref _currentProcessingStage, (int)stage);

    private void TrackHandledPublicationException(
        TickerState state,
        Exception exception,
        string publicationType)
    {
        Interlocked.Increment(ref _publicationFailures);
        var progress = Volatile.Read(ref _inFlightRecord);
        var stage = (TickAggregationProcessingStage)Volatile.Read(
            ref _currentProcessingStage);
        _logger.LogWarning(
            exception,
            "Tick aggregation handled a non-durable publication failure for {PublicationType}; " +
            "dataset {Dataset}, contract {ContractId}, record {RecordKind}, publisher {PublisherId}, " +
            "instrument {InstrumentId}, sequence {SourceSequence}, stage {ProcessingStage}. " +
            "The hot cache remains current and record processing will continue. " +
            "Publication failures {PublicationFailures}.",
            publicationType,
            progress?.Dataset ?? _options.Dataset,
            progress?.ContractId ?? state.Mapping.ContractId,
            progress?.RecordKind ?? string.Empty,
            progress?.PublisherId ?? state.Mapping.PublisherId,
            progress?.InstrumentId ?? state.Mapping.InstrumentId,
            progress?.SourceSequence ?? 0,
            stage,
            Interlocked.Read(ref _publicationFailures));
    }

    private static void UpdateMaximum(ref long target, long candidate)
    {
        var observed = Interlocked.Read(ref target);
        while (candidate > observed)
        {
            var prior = Interlocked.CompareExchange(ref target, candidate, observed);
            if (prior == observed) return;
            observed = prior;
        }
    }

    private void MarkObserved(TickerState state) =>
        MarkPublished(ref state.LastSourceRecordObservedUtcTicks);

    private void MarkAccepted(TickerState state, long sourceEventTimestampNanoseconds)
    {
        MarkPublished(ref state.LastAcceptedCacheUpdateUtcTicks);
        Interlocked.Exchange(
            ref state.LastAcceptedSourceEventUtcTicks,
            FromUnixNanoseconds(sourceEventTimestampNanoseconds).UtcTicks);
        Interlocked.Increment(ref state.AcceptedCacheUpdates);
    }

    private void MarkPublished(ref long target) =>
        Interlocked.Exchange(ref target, _timeProvider.GetUtcNow().UtcTicks);

    private static DateTimeOffset? ReadTimestamp(long utcTicks) =>
        utcTicks == 0
            ? null
            : new DateTimeOffset(utcTicks, TimeSpan.Zero);

    private static void ValidateOptions(TickAggregationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Dataset);
        if (options.DefinitionDate == default)
            throw new ArgumentOutOfRangeException(nameof(options.DefinitionDate));
        ValidateTimeout(options.FeedStartTimeout, nameof(options.FeedStartTimeout));
        ValidateTimeout(options.FeedStopTimeout, nameof(options.FeedStopTimeout));
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
        _generationStopping.Dispose();
        _lifecycle.Dispose();
    }

    private sealed class TickerState(TickContractMapping mapping)
    {
        public TickContractMapping Mapping { get; } = mapping;
        public Guid StreamEpochId { get; set; } = Guid.NewGuid();
        public MarketPriceCache MarketPrice { get; } = new(mapping);
        public FuturesSessionAccumulator SessionStatistics { get; } = new();
        public object StreamSync { get; } = new();
        public HashSet<TickerStreamOwner> StreamOwners { get; } = [];
        public DateOnly ValueDate;
        public long Sequence;
        public long TradeOrdinal;
        public ITickQuoteBufferLease? QuoteLease;
        public ushort QuoteCount;
        public PendingQuotePublication? PendingQuote;
        public PendingTradePublication? PendingTrade;
        public Dictionary<ushort, uint> HighestSourceSequenceByPublisher { get; } = [];
        public long LastSourceRecordObservedUtcTicks;
        public long LastMarketPricePublishedUtcTicks;
        public long LastDurableTickPublishedUtcTicks;
        public long StreamActivatedUtcTicks;
        public long LastAcceptedCacheUpdateUtcTicks;
        public long LastAcceptedSourceEventUtcTicks;
        public long AcceptedCacheUpdates;
        public long RejectedCacheUpdates;
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
