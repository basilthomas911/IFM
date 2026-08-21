using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>
/// Owns bounded, transient option-chain sessions. It has no tick-aggregation
/// event publisher or persistence dependency.
/// </summary>
public sealed class DatabentoOptionChainSessionManager :
    IDatabentoOptionChainSessionManager
{
    private const decimal PriceScale = 1_000_000_000m;
    private const long UndefinedPrice = long.MaxValue;
    private readonly object _sync = new();
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
    private readonly Dictionary<OptionChainSessionKey, Session> _sessions = [];
    private int _disposed;

    public DatabentoOptionChainSessionManager(
        IDatabentoFeedFactory feeds,
        DatabentoFeedOptions feedOptions,
        ITickAggregationService aggregation,
        IDatabentoLastPriceWriter lastPrices,
        IOptionChainGreeksEnricher enricher,
        IOptionChainTransientEventPublisher publisher,
        OptionChainStateStore state,
        int capacity = 8,
        TimeSpan? startTimeout = null,
        TimeSpan? stopTimeout = null,
        TimeSpan? pollTimeout = null)
    {
        _feeds = feeds ?? throw new ArgumentNullException(nameof(feeds));
        _feedOptions = feedOptions ?? throw new ArgumentNullException(nameof(feedOptions));
        _aggregation = aggregation ?? throw new ArgumentNullException(nameof(aggregation));
        _lastPrices = lastPrices ?? throw new ArgumentNullException(nameof(lastPrices));
        _enricher = enricher ?? throw new ArgumentNullException(nameof(enricher));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _startTimeout = startTimeout ?? TimeSpan.FromSeconds(30);
        _stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(30);
        _pollTimeout = pollTimeout ?? TimeSpan.FromMilliseconds(50);
    }

    public int ActiveSessionCount
    {
        get { lock (_sync) return _sessions.Count; }
    }

    public Task<bool> StartAsync(
        DatabentoOptionChainSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ValidateRequest(request);
        var key = new OptionChainSessionKey(
            request.FuturesContractId, request.Subscription.MaturityDate);
        var status = _aggregation.GetTickerStatus(request.FuturesContractId);
        if (!status.ServiceRunning || !status.TickerConfigured || !status.TickerRunning)
            throw new InvalidOperationException(
                $"Underlying tick aggregation is not running for '{request.FuturesContractId}'.");

        lock (_sync)
        {
            if (_sessions.TryGetValue(key, out var existing))
            {
                if (existing.HasSameSelection(request.Routes))
                    return Task.FromResult(false);
                throw new InvalidOperationException(
                    $"A conflicting option-chain session already exists for {key}.");
            }
            if (_sessions.Count >= _capacity)
                throw new InvalidOperationException(
                    $"The option-chain session capacity of {_capacity} has been reached.");

            var routes = request.Routes.ToDictionary(
                route => route.Definition.Instrument,
                route => route);
            foreach (var route in request.Routes)
                _lastPrices.RegisterContract(
                    route.FuturesOptionContractId,
                    AssetTypeId.FuturesOption);
            var feed = _feeds.CreateOptionChainFeed(_feedOptions);
            try
            {
                feed.Subscribe(request.Subscription, _startTimeout);
                feed.Start(_startTimeout);
                var session = new Session(key, request.ValueDate, feed, routes);
                _state.Create(key, request.Routes);
                _sessions.Add(key, session);
                session.Worker = Task.Run(() => ProcessAsync(session));
                return Task.FromResult(true);
            }
            catch
            {
                feed.Dispose();
                _state.Remove(key);
                throw;
            }
        }
    }

    public async Task<bool> StopAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(futuresContractId);
        var key = new OptionChainSessionKey(futuresContractId, maturityDate);
        Session? session;
        lock (_sync)
        {
            if (!_sessions.Remove(key, out session)) return false;
            session.StopRequested = true;
        }

        Exception? failure = null;
        try { session.Feed.Stop(_stopTimeout); }
        catch (Exception exception) { failure = exception; }
        try
        {
            if (session.Worker is not null)
                await session.Worker.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = failure is null
                ? exception
                : new AggregateException(failure, exception);
        }
        session.Feed.Dispose();
        _state.Remove(key);
        if (failure is not null) throw failure;
        return true;
    }

    private async Task ProcessAsync(Session session)
    {
        try
        {
            var reader = session.Feed.Reader;
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
                lock (_sync)
                {
                    if (_sessions.TryGetValue(session.Key, out var current)
                        && ReferenceEquals(current, session))
                        _sessions.Remove(session.Key);
                }
                _state.Remove(session.Key);
                session.Feed.Dispose();
            }
        }
    }

    private async ValueTask ProcessRecordAsync(Session session, MarketRecord64 record)
    {
        var instrument = new InstrumentKey(
            record.Header.PublisherId, record.Header.InstrumentId);
        if (!session.Routes.TryGetValue(instrument, out var route))
            throw new InvalidOperationException(
                $"The option-chain record instrument {instrument} is not mapped.");

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
                    FromUnixNanoseconds(quote.Header.ReceiveTimestampNanoseconds));
                var enriched = new LastQuoteTickWithGreeksSnapshot(
                    tick, _enricher.EnrichQuote(route, tick));
                _lastPrices.TryUpdateQuoteWithGreeks(enriched);
                _state.UpdateQuote(session.Key, route.FuturesOptionContractId, enriched);
                await _publisher.PublishAsync(new FuturesOptionChainQuoteChangedServiceEvent(
                    Guid.NewGuid(), session.Key.FuturesContractId,
                    route.FuturesOptionContractId, session.ValueDate,
                    session.Key.MaturityDate, tick, enriched.Greeks)).ConfigureAwait(false);
                break;
            }
            case MarketRecordKind.Trade:
            {
                var trade = record.Trade;
                var tick = new LastTradeTickSnapshot(
                    route.FuturesOptionContractId,
                    session.ValueDate,
                    trade.Price / PriceScale,
                    trade.Size,
                    trade.Header.Sequence,
                    FromUnixNanoseconds(trade.Header.EventTimestampNanoseconds),
                    FromUnixNanoseconds(trade.Header.ReceiveTimestampNanoseconds));
                var enriched = new LastTradeTickWithGreeksSnapshot(
                    tick, _enricher.EnrichTrade(route, tick));
                _lastPrices.TryUpdateTradeWithGreeks(enriched);
                _state.UpdateTrade(session.Key, route.FuturesOptionContractId, enriched);
                await _publisher.PublishAsync(new FuturesOptionChainTradeChangedServiceEvent(
                    Guid.NewGuid(), session.Key.FuturesContractId,
                    route.FuturesOptionContractId, session.ValueDate,
                    session.Key.MaturityDate, tick, enriched.Greeks)).ConfigureAwait(false);
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        OptionChainSessionKey[] sessions;
        lock (_sync) sessions = _sessions.Keys.ToArray();
        List<Exception>? failures = null;
        foreach (var session in sessions)
        {
            try { await StopAsync(session.FuturesContractId, session.MaturityDate).ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is not null)
            throw new AggregateException("Option-chain session shutdown failed.", failures);
    }

    private static void ValidateRequest(DatabentoOptionChainSessionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FuturesContractId);
        if (request.ValueDate == default) throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentNullException.ThrowIfNull(request.Subscription);
        ArgumentNullException.ThrowIfNull(request.Routes);
        if (request.Routes.Count == 0)
            throw new ArgumentException("At least one resolved option route is required.", nameof(request));
        if (request.Routes.Select(route => route.FuturesOptionContractId)
            .Distinct(StringComparer.Ordinal).Count() != request.Routes.Count)
            throw new ArgumentException("Option route domain IDs must be unique.", nameof(request));
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

    private sealed class Session(
        OptionChainSessionKey key,
        DateOnly valueDate,
        IDatabentoOptionChainFeed feed,
        Dictionary<InstrumentKey, DatabentoOptionChainRoute> routes)
    {
        internal OptionChainSessionKey Key { get; } = key;
        internal DateOnly ValueDate { get; } = valueDate;
        internal IDatabentoOptionChainFeed Feed { get; } = feed;
        internal Dictionary<InstrumentKey, DatabentoOptionChainRoute> Routes { get; } = routes;
        internal Task? Worker;
        internal bool StopRequested;

        internal bool HasSameSelection(IReadOnlyList<DatabentoOptionChainRoute> candidates) =>
            Routes.Values.Select(route => route.FuturesOptionContractId)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    candidates.Select(route => route.FuturesOptionContractId)
                        .Order(StringComparer.Ordinal),
                    StringComparer.Ordinal);
    }
}
