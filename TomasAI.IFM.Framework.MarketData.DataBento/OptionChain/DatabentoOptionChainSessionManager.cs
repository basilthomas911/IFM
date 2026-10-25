using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>
/// Owns bounded logical discovery sessions sharing one physical connection per
/// underlying and value date. Explicit option-leg sessions retain dedicated connections.
/// </summary>
public sealed class DatabentoOptionChainSessionManager :
    IDatabentoOptionChainSessionManager
{
    private const decimal PriceScale = 1_000_000_000m;
    private const long UndefinedPrice = long.MaxValue;
    private const ushort ClearedVolumeStatistic = 6;
    private const ushort OpenInterestStatistic = 9;
    private const byte NewStatistic = 1;
    private const long UndefinedStatisticQuantity = long.MaxValue;
    private readonly System.Threading.Lock _sync = new();
    private readonly IDatabentoFeedFactory _feeds;
    private readonly DatabentoFeedOptions _feedOptions;
    private readonly ITickAggregationService _aggregation;
    private readonly IDatabentoLastPriceWriter _lastPrices;
    private readonly IOptionChainGreeksEnricher _enricher;
    private readonly IOptionChainTransientEventPublisher _publisher;
    private readonly OptionChainStateStore _state;
    private readonly int _capacity;
    private readonly TimeSpan _startTimeout;
    private readonly TimeSpan _stopTimeout;
    private readonly TimeSpan _pollTimeout;
    private readonly TimeProvider _time;
    private readonly Dictionary<OptionChainSessionKey, Session> _sessions = [];
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2213",Justification="Lifecycle waiters may still observe this managed semaphore after disposal begins; it has no allocated WaitHandle and is reclaimed with the consumer.")]
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private int _disposed;

    /// <summary>Initializes a new DatabentoOptionChainSessionManager instance.</summary>
    /// <param name="feeds">The factory used to create option-chain feeds.</param>
    /// <param name="feedOptions">The feed options.</param>
    /// <param name="aggregation">The tick aggregation service used to integrate the option-chain session.</param>
    /// <param name="lastPrices">The value-date-specific cache of accepted trade and quote prices.</param>
    /// <param name="enricher">The component adding calculated Greeks to accepted option prices.</param>
    /// <param name="publisher">The destination used to publish tick or option-chain events.</param>
    /// <param name="state">The store holding the option-chain session state.</param>
    /// <param name="capacity">The maximum capacity of the buffer, store, or queue.</param>
    /// <param name="startTimeout">The maximum duration allowed to start a session.</param>
    /// <param name="stopTimeout">The maximum duration allowed to stop and drain a session.</param>
    /// <param name="pollTimeout">The maximum time spent waiting for the next feed batch.</param>
    /// <param name="time">The local ingestion clock, independent of the provider event and receive clocks.</param>
    public DatabentoOptionChainSessionManager(
        IDatabentoFeedFactory feeds,
        DatabentoFeedOptions feedOptions,
        ITickAggregationService aggregation,
        IDatabentoLastPriceWriter lastPrices,
        IOptionChainGreeksEnricher enricher,
        IOptionChainTransientEventPublisher publisher,
        OptionChainStateStore state,
        int capacity = 16,
        TimeSpan? startTimeout = null,
        TimeSpan? stopTimeout = null,
        TimeSpan? pollTimeout = null,
        TimeProvider? time = null)
    {
        _feeds = feeds ?? throw new ArgumentNullException(nameof(feeds));
        _feedOptions = feedOptions ?? throw new ArgumentNullException(nameof(feedOptions));
        _aggregation = aggregation ?? throw new ArgumentNullException(nameof(aggregation));
        _lastPrices = lastPrices ?? throw new ArgumentNullException(nameof(lastPrices));
        _enricher = enricher ?? throw new ArgumentNullException(nameof(enricher));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
_capacity = capacity;
        _startTimeout = startTimeout ?? TimeSpan.FromSeconds(30);
        _stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(30);
        _pollTimeout = pollTimeout ?? TimeSpan.FromMilliseconds(50);
        _time = time ?? TimeProvider.System;
    }

    public int ActiveSessionCount
    {
        get { lock (_sync) return _sessions.Count; }
    }

    /// <summary>Starts the market data component asynchronously.</summary>
    /// <param name="request">The parameters identifying the requested provider data or session.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep validation, native ownership and failure ordering in one method for this compatibility pass.")]
    public async Task<bool> StartAsync(
        DatabentoOptionChainSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        if (!string.IsNullOrEmpty(request.OptionContractId) &&
            (request.Routes.Count != 1 || request.Routes[0].FuturesOptionContractId != request.OptionContractId))
            throw new ArgumentException("An isolated option connection requires exactly its named contract.", nameof(request));
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var key = new OptionChainSessionKey(request.FuturesContractId,
                request.Subscription.MaturityDate, request.OptionContractId, request.ConnectionScopeId);
            var status = _aggregation.GetTickerStatus(request.FuturesContractId);
            if (!status.ServiceRunning || !status.TickerConfigured || !status.TickerRunning)
                throw new InvalidOperationException($"Underlying tick aggregation is not running for '{request.FuturesContractId}'.");
            Session? shared;
            lock (_sync)
            {
                if (_sessions.TryGetValue(key, out var existing))
                {
                    if (existing.Selections[key].Select(x => x.FuturesOptionContractId).Order(StringComparer.Ordinal)
                        .SequenceEqual(request.Routes.Select(x => x.FuturesOptionContractId).Order(StringComparer.Ordinal))) return false;
                    throw new InvalidOperationException($"A conflicting option-chain session already exists for {key}.");
                }
                if (_sessions.Count >= _capacity)
                    throw new InvalidOperationException($"The option-chain session capacity of {_capacity} has been reached.");
                shared = string.IsNullOrEmpty(request.OptionContractId)
                    ? _sessions.Values.FirstOrDefault(x => string.IsNullOrEmpty(x.Key.OptionContractId)
                        && x.Key.FuturesContractId == key.FuturesContractId && x.ValueDate == request.ValueDate)
                    : null;
            }
            foreach (var route in request.Routes)
                _lastPrices.RegisterContract(route.FuturesOptionContractId, AssetTypeId.FuturesOption);
            if (shared is not null && request.Routes.All(x => shared.Routes.TryGetValue(x.Definition.Instrument.InstrumentId, out var route)
                    && route == x) && (shared.Subscription.DataKinds & request.Subscription.DataKinds) == request.Subscription.DataKinds)
            {
                lock (_sync)
                {
                    try
                    {
                        _state.Create(key, request.Routes);
                        foreach (var route in request.Routes)
                        {
                            // Physical unions retain routes after their final logical owner detaches.
                            // Such a route has no state to copy; saved statistics/cache seed the new owner.
                            var source = shared.InterestedKeys[route.Definition.Instrument.InstrumentId].FirstOrDefault();
                            if (source != default) _state.CopyStatistics(source, key, route.FuturesOptionContractId);
                        }
                        _state.Seed(key, request.SavedStatistics, request.ValueDate);
                        shared.Selections.Add(key, request.Routes);
                        foreach (var route in request.Routes) shared.InterestedKeys[route.Definition.Instrument.InstrumentId].Add(key);
                        _sessions.Add(key, shared);
                        foreach (var route in request.Routes)
                        {
                            var reader = _lastPrices as IDatabentoLastPriceStore;
                            if (reader is null) continue;
                            var prices = reader.GetFuturesOptionReader(route.FuturesOptionContractId, request.ValueDate);
                            if (prices.TryGetLastQuoteWithGreeks(out var quote)) _state.UpdateQuote(key, route.FuturesOptionContractId, quote);
                        }
                    }
                    catch
                    {
                        _sessions.Remove(key);
                        shared.Selections.Remove(key);
                        foreach (var owners in shared.InterestedKeys.Values) owners.Remove(key);
                        _state.Remove(key);
                        throw;
                    }
                }
                return true;
            }
            var selections = shared is null
                ? new Dictionary<OptionChainSessionKey, IReadOnlyList<DatabentoOptionChainRoute>>()
                : new Dictionary<OptionChainSessionKey, IReadOnlyList<DatabentoOptionChainRoute>>(shared.Selections);
            selections.Add(key, request.Routes);
            var routes = selections.Values.SelectMany(x => x)
                .GroupBy(x => x.Definition.Instrument.InstrumentId).ToDictionary(x => x.Key, x => x.First());
            if (routes.Count > 2048) throw new InvalidOperationException("The shared discovery connection capacity of 2048 contracts has been reached.");
            foreach (var route in request.Routes)
                if (shared is not null && shared.Routes.TryGetValue(route.Definition.Instrument.InstrumentId, out var previous) && previous != route)
                    throw new InvalidOperationException("Conflicting definitions for a shared provider instrument.");
            var subscription = request.Subscription with
            {
                ResolvedContracts = routes.Values.Select(x => x.Definition).ToArray(),
                Strikes = routes.Values.Select(x => x.Definition.StrikePrice).Distinct().ToArray(),
                Rights = routes.Values.Aggregate(OptionRightSelection.None, (rights, x) => rights | x.Definition.Right),
                DataKinds = request.Subscription.DataKinds | (shared?.Subscription.DataKinds ?? MarketDataKinds.None),
                MultipleMaturities = routes.Values.Select(x => x.Definition.MaturityDate).Distinct().Skip(1).Any()
            };
            // Native subscriptions are immutable. Stop before replacement so expansion never
            // temporarily consumes another account connection. Existing logical states survive.
            if (shared is not null)
            {
                try { await StopPhysicalAsync(shared).ConfigureAwait(false); }
                catch { RemovePhysical(shared); throw; }
            }
            try
            {
                _state.Create(key, request.Routes);
                _state.Seed(key, request.SavedStatistics, request.ValueDate);
                StartPhysical(key, request.ValueDate, subscription, routes, selections);
            }
            catch (Exception startupFailure)
            {
                _state.Remove(key);
                if (shared is not null)
                {
                    try { StartPhysical(shared.Key, shared.ValueDate, shared.Subscription, shared.Routes, shared.Selections); }
                    catch (Exception restoreFailure)
                    {
                        RemovePhysical(shared);
                        throw new AggregateException("Discovery expansion and restoration both failed.", startupFailure, restoreFailure);
                    }
                }
                throw;
            }
            return true;
        }
        finally { _lifecycle.Release(); }
    }

    private void StartPhysical(OptionChainSessionKey key, DateOnly valueDate,
        OptionChainSubscription subscription, Dictionary<uint, DatabentoOptionChainRoute> routes,
        Dictionary<OptionChainSessionKey, IReadOnlyList<DatabentoOptionChainRoute>> selections)
    {
        // Replay is streamed by the consumer after mapping acknowledgement; quote readiness does not wait for statistics.
        var now = _time.GetUtcNow();
        var earliest = now.AddHours(-23).AddMinutes(-55); // Stay within the provider's 24-hour replay boundary.
        var sessionStart = TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetSessionStartUtc(valueDate);
        var tradeStart = sessionStart > earliest ? sessionStart : earliest;
        if (tradeStart > now) tradeStart = now;
        var feed = _feeds.CreateOptionChainFeed(_feedOptions with
        {
            StatisticsReplayStartTimestampNanoseconds = ToUnixNanoseconds(earliest),
            TradeReplayStartTimestampNanoseconds = ToUnixNanoseconds(tradeStart)
        });
        Session? session = null;
        try
        {
            feed.Subscribe(subscription, _startTimeout);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            feed.Start(_startTimeout, remaining =>
            {
                session = new Session(key, valueDate, feed, routes, subscription, selections) { FullSessionReplay = tradeStart == sessionStart };
                lock (_sync) foreach (var logicalKey in selections.Keys) _sessions[logicalKey] = session;
                session.Worker = Task.Run(() => ProcessAsync(session, ready));
                if (!ready.Task.Wait(remaining)) throw new TimeoutException("The option-chain consumer did not become ready before feed activation.");
            });
        }
        catch
        {
            if (session is not null)
            {
                session.StopRequested = true;
                lock (_sync) foreach (var logicalKey in selections.Keys)
                    if (_sessions.TryGetValue(logicalKey, out var current) && ReferenceEquals(current, session)) _sessions.Remove(logicalKey);
            }
            feed.Dispose();
            throw;
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]

    private async Task StopPhysicalAsync(Session session)
    {
        session.StopRequested = true;
        Exception? failure = null;
        try { session.Feed.Stop(_stopTimeout); }
        catch (Exception exception) { failure = exception; }
        try { if (session.Worker is not null) await session.Worker.WaitAsync(_stopTimeout).ConfigureAwait(false); }
        catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        finally { session.Feed.Dispose(); }
        if (failure is not null) throw failure;
    }

    private void RemovePhysical(Session session)
    {
        lock (_sync)
        {
            foreach (var key in session.Selections.Keys)
                if (!_sessions.TryGetValue(key, out var current) || ReferenceEquals(current, session))
                { _sessions.Remove(key); _state.Remove(key); }
        }
    }

    /// <summary>Stops the chain connection while leaving independently owned option-leg connections active.</summary>
    /// <param name="futuresContractId">The underlying futures contract.</param>
    /// <param name="maturityDate">The chain maturity.</param>
    /// <returns>Whether a chain connection was removed.</returns>
    public Task<bool> StopAsync(string futuresContractId, DateOnly maturityDate)
        => StopAsync(futuresContractId, maturityDate, "");

    /// <summary>Stops the market data component asynchronously.</summary>
    /// <param name="futuresContractId">The underlying futures contract identifier.</param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="optionContractId">The isolated leg contract, or empty for the chain connection.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Collect stop and worker failures while completing owned feed cleanup, then rethrow the collected failure.")]
    public async Task<bool> StopAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string optionContractId, string connectionScopeId = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(futuresContractId);
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            var key = new OptionChainSessionKey(futuresContractId, maturityDate, optionContractId, connectionScopeId);
            Session? session;
            lock (_sync)
            {
                if (!_sessions.Remove(key, out session)) return false;
                session.Selections.Remove(key);
                foreach (var owners in session.InterestedKeys.Values) owners.Remove(key);
                _state.Remove(key);
                if (session.Selections.Count != 0) return true;
            }
            await StopPhysicalAsync(session).ConfigureAwait(false);
            return true;
        }
        finally { _lifecycle.Release(); }
    }

    private async Task ProcessAsync(
        Session session,
        TaskCompletionSource? startupReady = null)
    {
        try
        {
            var reader = session.Feed.Reader;
            startupReady?.TrySetResult();
            while (true)
            {
                if (!reader.TryRead(_pollTimeout, out var batch))
                {
                    if (reader.IsCompleted) break;
                    var status = _aggregation.GetTickerStatus(session.Key.FuturesContractId);
                    if (!status.ServiceRunning || !status.TickerRunning)
                        session.Feed.Stop(_stopTimeout);
                    continue;
                }

                using var leased = batch!;
                for (var index = 0; index < leased.Count; index++)
                {
                    await ProcessRecordAsync(session, leased.Records[index])
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (!session.StopRequested)
            {
                RemovePhysical(session);
                session.Feed.Dispose();
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep validation, native ownership and failure ordering in one method for this compatibility pass.")]
    private async ValueTask ProcessRecordAsync(Session session, MarketRecord64 record)
    {
        if (record.Header.RecordKind == MarketRecordKind.StatisticsReplayComplete) return;
        if (record.Header.RecordKind == MarketRecordKind.TradeReplayComplete)
        {
            // Native replay completion is emitted once per instrument, not once per logical owner.
            if (session.FullSessionReplay && session.Routes.TryGetValue(record.Header.InstrumentId, out var completed))
                UpdateLogicalStates(session, completed, key => _state.CompleteVolumeReplay(key, completed.FuturesOptionContractId, session.ValueDate));
            return;
        }
        if (!session.Routes.TryGetValue(record.Header.InstrumentId, out var route))
            throw new InvalidOperationException(
                $"The option-chain record instrument {record.Header.InstrumentId} is not mapped.");

        switch (record.Header.RecordKind)
        {
            case MarketRecordKind.Quote:
                {
                    var quote = record.Quote;
                    var tick = new LastQuoteTickSnapshot(
                        route.FuturesOptionContractId,
                        session.ValueDate,
                        ScaleNullable(quote.BidPrice),
                        quote.BidSize,
                        quote.BidCount,
                        ScaleNullable(quote.AskPrice),
                        quote.AskSize,
                        quote.AskCount,
                        quote.Header.Sequence,
                        FromUnixNanoseconds(quote.Header.EventTimestampNanoseconds),
                        FromUnixNanoseconds(quote.Header.ReceiveTimestampNanoseconds))
                    {
                        LocalReceivedAtUtc = _time.GetUtcNow()
                    };
                    var enriched = new LastQuoteTickWithGreeksSnapshot(
                        tick, _enricher.EnrichQuote(route, tick));
                    if (!_lastPrices.TryUpdateQuoteWithGreeks(enriched))
                        break;
                    UpdateLogicalStates(session, route, key => _state.UpdateQuote(key, route.FuturesOptionContractId, enriched));
                    await _publisher.PublishAsync(new FuturesOptionChainQuoteChangedServiceEvent(
                        Guid.NewGuid(), session.Key.FuturesContractId,
                        route.FuturesOptionContractId, session.ValueDate,
                        route.Definition.MaturityDate, tick, enriched.Greeks)).ConfigureAwait(false);
                    break;
                }
            case MarketRecordKind.Trade:
                {
                    var trade = record.Trade;
                    if (trade.Price != UndefinedPrice && trade.Action is not ((byte)'C') and not ((byte)'R'))
                        UpdateLogicalStates(session, route, key => _state.AccumulateVolume(key, route.FuturesOptionContractId,
                            trade.Size, trade.Header.EventTimestampNanoseconds, trade.Header.Sequence, session.ValueDate));
                    // Historical volume reconstruction must not trigger pricing evidence, risk callbacks or trade workflows.
                    if ((trade.Header.Flags & 2) != 0) break;
                    var tick = new LastTradeTickSnapshot(
                        route.FuturesOptionContractId,
                        session.ValueDate,
                        trade.Price / PriceScale,
                        trade.Size,
                        trade.Header.Sequence,
                        FromUnixNanoseconds(trade.Header.EventTimestampNanoseconds),
                        FromUnixNanoseconds(trade.Header.ReceiveTimestampNanoseconds));
                    var enriched = new LastTradeTickWithGreeksSnapshot(
                        tick, _enricher is IRetainedOptionTradeEnricher retained
                            ? await retained.EnrichTradeAsync(route, tick, trade.Header.EventTimestampNanoseconds,
                                trade.Header.ReceiveTimestampNanoseconds, CancellationToken.None).ConfigureAwait(false)
                            : _enricher.EnrichTrade(route, tick));
                    if (!_lastPrices.TryUpdateTradeWithGreeks(enriched))
                        break;
                    UpdateLogicalStates(session, route, key => _state.UpdateTrade(key, route.FuturesOptionContractId, enriched));
                    await _publisher.PublishAsync(new FuturesOptionChainTradeChangedServiceEvent(
                        Guid.NewGuid(), session.Key.FuturesContractId,
                        route.FuturesOptionContractId, session.ValueDate,
                        route.Definition.MaturityDate, tick, enriched.Greeks)).ConfigureAwait(false);
                    break;
                }
            case MarketRecordKind.Statistics:
                ApplyStatistics(session, route, record.Statistics);
                break;
        }
    }

    /// <summary>Asynchronously stops processing and releases the resources owned by this instance.</summary>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Complete cleanup of all sessions and rethrow collected disposal failures.")]
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        OptionChainSessionKey[] sessions;
        lock (_sync) sessions = _sessions.Keys.ToArray();
        List<Exception>? failures = null;
        foreach (var session in sessions)
        {
            try { await StopAsync(session.FuturesContractId, session.MaturityDate, session.OptionContractId, session.ConnectionScopeId).ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is not null)
            throw new AggregateException("Option-chain session shutdown failed.", failures);
    }

    private static ulong ToUnixNanoseconds(DateTimeOffset instant) =>
        checked((ulong)(instant.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL);

    private void ApplyStatistics(Session session, DatabentoOptionChainRoute route, StatisticsRecord64 record)
    {
        if (record.UpdateAction != NewStatistic || record.Quantity < 0
            || record.Quantity == UndefinedStatisticQuantity) return;
        var referenceDate = DateOnly.FromDateTime(
            FromUnixNanoseconds(record.ReferenceTimestampNanoseconds).UtcDateTime);
        if (referenceDate > session.ValueDate) return;
        long? volume = record.StatisticType == ClearedVolumeStatistic ? record.Quantity : null;
        long? interest = record.StatisticType == OpenInterestStatistic ? record.Quantity : null;
        if (volume is null && interest is null) return;
        UpdateLogicalStates(session, route, key => _state.UpdateStatistics(key, route.FuturesOptionContractId, volume, interest, referenceDate,
            FromUnixNanoseconds(record.Header.EventTimestampNanoseconds)));
    }

    private void UpdateLogicalStates(Session session, DatabentoOptionChainRoute route, Action<OptionChainSessionKey> update)
    {
        lock (_sync)
            if (session.InterestedKeys.TryGetValue(route.Definition.Instrument.InstrumentId, out var owners))
                foreach (var key in owners)
                    if (_sessions.TryGetValue(key, out var current) && ReferenceEquals(current, session)) update(key);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation names the public request that owns the invalid property rather than an inaccessible nested field.")]
    private static void ValidateRequest(DatabentoOptionChainSessionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FuturesContractId, nameof(request));
        if (request.ValueDate == default) throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentNullException.ThrowIfNull(request.Subscription, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Routes, nameof(request));
        if (request.Routes.Count == 0)
            throw new ArgumentException("At least one resolved option route is required.", nameof(request));
        if (request.Routes.Select(route => route.FuturesOptionContractId)
            .Distinct(StringComparer.Ordinal).Count() != request.Routes.Count)
            throw new ArgumentException("Option route domain IDs must be unique.", nameof(request));
        if (request.Routes.Select(route => route.Definition.Instrument.InstrumentId)
            .Distinct().Count() != request.Routes.Count)
            throw new ArgumentException("Option route instrument IDs must be unique.", nameof(request));
        if (request.Routes.Any(route =>
                route.Definition.MaturityDate != request.Subscription.MaturityDate
                || !request.Subscription.ResolvedContracts.Contains(route.Definition)))
            throw new ArgumentException(
                "Every route must belong to the immutable provider subscription.", nameof(request));
    }

    private static decimal? ScaleNullable(long value) =>
        value == UndefinedPrice ? null : value / PriceScale;

    private static DateTimeOffset FromUnixNanoseconds(long nanoseconds)
    {
        try { return DateTimeOffset.UnixEpoch.AddTicks(nanoseconds / 100L); }
        catch (ArgumentOutOfRangeException)
        {
            return nanoseconds < 0 ? DateTimeOffset.MinValue : DateTimeOffset.MaxValue;
        }
    }

    /// <summary>Initializes a new Session instance.</summary>
    /// <param name="key">The identifier of the option-chain session.</param>
    /// <param name="valueDate">The trading value date associated with the data.</param>
    /// <param name="feed">The feed supplying market data batches.</param>
    /// <param name="routes">The resolved instrument routes used by this option-chain session.</param>
    private sealed class Session(
        OptionChainSessionKey key,
        DateOnly valueDate,
        IDatabentoOptionChainFeed feed,
        Dictionary<uint, DatabentoOptionChainRoute> routes,
        OptionChainSubscription subscription,
        Dictionary<OptionChainSessionKey, IReadOnlyList<DatabentoOptionChainRoute>> selections)
    {
        internal OptionChainSessionKey Key { get; } = key;
        internal DateOnly ValueDate { get; } = valueDate;
        internal IDatabentoOptionChainFeed Feed { get; } = feed;
        internal Dictionary<uint, DatabentoOptionChainRoute> Routes { get; } = routes;
        internal OptionChainSubscription Subscription { get; } = subscription;
        internal Dictionary<OptionChainSessionKey, IReadOnlyList<DatabentoOptionChainRoute>> Selections { get; } = selections;
        internal Dictionary<uint, HashSet<OptionChainSessionKey>> InterestedKeys { get; } = selections
            .SelectMany(pair => pair.Value.Select(route => (Instrument: route.Definition.Instrument.InstrumentId, Key: pair.Key)))
            .GroupBy(x => x.Instrument).ToDictionary(group => group.Key, group => group.Select(x => x.Key).ToHashSet());
        internal Task? Worker;
        internal bool StopRequested;
        internal bool FullSessionReplay;

        internal bool HasSameSelection(IReadOnlyList<DatabentoOptionChainRoute> candidates) =>
            Routes.Values.Select(route => route.FuturesOptionContractId)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    candidates.Select(route => route.FuturesOptionContractId)
                        .Order(StringComparer.Ordinal),
                    StringComparer.Ordinal);
    }
}
