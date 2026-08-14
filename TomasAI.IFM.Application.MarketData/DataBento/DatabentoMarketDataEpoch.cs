using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed class DatabentoMarketDataEpochFactory : IDatabentoMarketDataEpochFactory
{
    private readonly IDatabentoFeedFactory _feeds;
    private readonly ITickAggregationEventPublisher _publisher;
    private readonly DatabentoMarketDataRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ITickLiveEventPublisher _livePublisher;

    public DatabentoMarketDataEpochFactory(
        IDatabentoFeedFactory feeds,
        ITickAggregationEventPublisher publisher,
        DatabentoMarketDataRuntimeOptions options,
        TimeProvider? timeProvider = null,
        ITickLiveEventPublisher? livePublisher = null)
    {
        _feeds = feeds ?? throw new ArgumentNullException(nameof(feeds));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _livePublisher = livePublisher ?? new NullTickLiveEventPublisher();
    }

    public IDatabentoMarketDataEpoch Create(DateOnly valueDate) =>
        new DatabentoMarketDataEpoch(
            valueDate, _feeds, _publisher, _options, _timeProvider, _livePublisher);
}

internal sealed class DatabentoMarketDataEpoch : IDatabentoMarketDataEpoch
{
    private readonly IDatabentoFeedFactory _feeds;
    private readonly ITickAggregationEventPublisher _publisher;
    private readonly DatabentoMarketDataRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly DatabentoOptionRouteRegistry _optionRoutes;
    private readonly ITickLiveRouter _liveRouter;
    private readonly ITickerLeaseRouteController _leaseRoutes;
    private readonly Dictionary<string, ITickerDataReader> _compatibilityReaders =
        new(StringComparer.Ordinal);
    private readonly object _compatibilityReaderSync = new();
    private DatabentoOperationRunner? _operations;
    private DatabentoMarketDataCatalog? _catalog;
    private DatabentoLastPriceStore? _lastPrices;
    private TickAggregationService? _aggregation;
    private int _started;
    private int _disposed;

    internal DatabentoMarketDataEpoch(
        DateOnly valueDate,
        IDatabentoFeedFactory feeds,
        ITickAggregationEventPublisher publisher,
        DatabentoMarketDataRuntimeOptions options,
        TimeProvider timeProvider,
        ITickLiveEventPublisher livePublisher)
    {
        if (valueDate == default) throw new ArgumentOutOfRangeException(nameof(valueDate));
        ValueDate = valueDate;
        _feeds = feeds;
        _publisher = publisher;
        _options = options;
        _timeProvider = timeProvider;
        _optionRoutes = new DatabentoOptionRouteRegistry(options.MaximumConcurrentOptionChains);
        _liveRouter = new TickLiveRouter(livePublisher);
        _leaseRoutes = new DatabentoTickerLeaseRouteController(_liveRouter, _optionRoutes);
    }

    public DateOnly ValueDate { get; }
    public IDatabentoMarketDataCatalog Catalog =>
        _catalog ?? throw new InvalidOperationException("The epoch catalog is not ready.");
    public IDatabentoLastPriceReaderProvider LastPrices =>
        _lastPrices ?? throw new InvalidOperationException("The epoch last-price store is not ready.");
    public ITickerDataReaderFactory TickerReaders =>
        _aggregation ?? throw new InvalidOperationException("Tick aggregation is not ready.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _started) != 0) return;
            ValidateRuntimeOptions(_options);
            var queryClients = Enumerable.Range(0, _options.QueryConcurrency)
                .Select(_ => _feeds.CreateMarketDataQueries(_options.FeedOptions))
                .ToArray();
            _operations = new DatabentoOperationRunner(
                queryClients, _options.QueryQueueCapacity);
            _catalog = await DatabentoMarketDataCatalog.CreateAsync(
                _operations, _options, cancellationToken).ConfigureAwait(false);

            _lastPrices = new DatabentoLastPriceStore(
                ValueDate, _options.LastPriceCapacity);
            var mappings = new DatabentoTickContractMappingStore();
            foreach (var resolved in _catalog.ResolvedContracts)
            {
                mappings.SetTickMapping(
                    _options.FeedOptions.Dataset,
                    ValueDate,
                    resolved.Detail.Instrument.PublisherId,
                    resolved.Detail.Instrument.InstrumentId,
                    resolved.Registration.DomainContractId,
                    resolved.Registration.AssetTypeId,
                    CreateContractDetails(resolved));
                _lastPrices.RegisterContract(
                    resolved.Registration.DomainContractId,
                    resolved.Registration.AssetTypeId);
            }

            var feed = _feeds.CreateTickerFeed(_options.FeedOptions);
            try
            {
                var subscriptions = _catalog.ResolvedContracts
                    .Select(resolved => new TickerSubscription(
                        resolved.Detail.RawSymbol,
                        DatabentoInputSymbology.RawSymbol,
                        MarketDataKinds.Quote | MarketDataKinds.Trade))
                    .ToArray();
                feed.Subscribe(subscriptions, _options.ProviderQueryTimeout);
                _aggregation = new TickAggregationService(
                    feed,
                    mappings,
                    _publisher,
                    new TickQuoteBufferPool(),
                    new EpochValueDateProvider(ValueDate),
                    new TickAggregationOptions
                    {
                        Dataset = _options.FeedOptions.Dataset,
                        DefinitionDate = ValueDate,
                        FeedStartTimeout = _options.FeedStartTimeout,
                        FeedStopTimeout = _options.FeedStopTimeout,
                        ReaderPollTimeout = _options.ReaderPollTimeout
                    },
                    _timeProvider,
                    _lastPrices,
                    _liveRouter,
                    _leaseRoutes);
                await _aggregation.StartAsync().ConfigureAwait(false);
            }
            catch
            {
                if (_aggregation is null) feed.Dispose();
                throw;
            }

            Volatile.Write(ref _started, 1);
        }
        finally { _lifecycle.Release(); }
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            _optionRoutes.Clear();
            _liveRouter.Clear();
            lock (_compatibilityReaderSync)
                _compatibilityReaders.Clear();
            List<Exception>? failures = null;
            if (_aggregation is not null)
            {
                try { await _aggregation.StopAsync().ConfigureAwait(false); }
                catch (Exception exception) { (failures ??= []).Add(exception); }
            }
            _lastPrices?.Invalidate();
            Volatile.Write(ref _started, 0);
            if (failures is not null)
                throw new AggregateException("The DataBento epoch stop failed.", failures);
        }
        finally { _lifecycle.Release(); }
    }

    public TickAggregationContractStatus GetAggregationStatus(string contractId) =>
        _aggregation?.GetContractStatus(contractId)
        ?? new TickAggregationContractStatus(
            contractId, AssetTypeId.Unknown, false, false, false);

    public DatabentoMarketDataEpochHealth GetHealth()
    {
        var metrics = _aggregation?.GetMetrics() ?? default;
        return new DatabentoMarketDataEpochHealth(
            ValueDate,
            Volatile.Read(ref _started) != 0,
            _aggregation?.IsRunning == true,
            _catalog?.ResolvedContracts.Count ?? 0,
            _lastPrices?.Count ?? 0,
            _lastPrices?.IsActive == true,
            metrics.SourceQuoteRecords,
            metrics.SourceTradeRecords,
            metrics.PublicationFailures);
    }

    public bool StartFuturesRoute(string futuresContractId)
    {
        EnsureRunning();
        return StartCompatibilityReader(futuresContractId, "futures");
    }

    public bool StopFuturesRoute(string futuresContractId)
    {
        EnsureRunning();
        return StopCompatibilityReader(futuresContractId);
    }

    public bool StartIndividualOptionRoute(string futuresOptionContractId)
    {
        EnsureRunning();
        return StartCompatibilityReader(futuresOptionContractId, "option");
    }

    public bool StopIndividualOptionRoute(string futuresOptionContractId)
    {
        EnsureRunning();
        return StopCompatibilityReader(futuresOptionContractId);
    }

    public Task<bool> StartOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds)
    {
        EnsureRunning();
        // Phase A deliberately performs no reservation or provider allocation:
        // the immutable FMP-derived session rate is a required start input.
        throw new MarketDataPricingInputUnavailableException("Treasury curve session rate");
    }

    public Task<bool> StopOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        EnsureRunning();
        return Task.FromResult(_optionRoutes.ReleaseChain(futuresContractId, maturityDate));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { await StopAsync().ConfigureAwait(false); }
        catch { /* Disposal continues so native and managed resources are released. */ }
        if (_aggregation is not null)
            await _aggregation.DisposeAsync().ConfigureAwait(false);
        if (_operations is not null)
            await _operations.DisposeAsync().ConfigureAwait(false);
        _lastPrices?.Dispose();
        _lifecycle.Dispose();
    }

    private void EnsureRunning()
    {
        if (Volatile.Read(ref _started) == 0)
            throw new MarketDataApiNotRunningException();
    }

    private bool StartCompatibilityReader(string contractId, string legId)
    {
        lock (_compatibilityReaderSync)
        {
            if (_compatibilityReaders.ContainsKey(contractId)) return false;
            var owner = new TickerReaderOwner(
                nameof(DatabentoMarketDataApi),
                $"compatibility:{ValueDate:yyyy-MM-dd}",
                legId);
            var reader = TickerReaders.CreateAsync(owner, contractId)
                .AsTask().GetAwaiter().GetResult();
            _compatibilityReaders.Add(contractId, reader);
            return true;
        }
    }

    private bool StopCompatibilityReader(string contractId)
    {
        ITickerDataReader? reader;
        lock (_compatibilityReaderSync)
        {
            if (!_compatibilityReaders.Remove(contractId, out reader)) return false;
        }
        reader.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return true;
    }

    private TickerContractDetails CreateContractDetails(
        DatabentoMarketDataCatalog.ResolvedContract resolved)
    {
        var detail = resolved.Detail;
        return new TickerContractDetails
        {
            ContractId = resolved.Registration.DomainContractId,
            InstrumentId = detail.Instrument.InstrumentId,
            PublisherId = detail.Instrument.PublisherId,
            AssetTypeId = resolved.Registration.AssetTypeId,
            Dataset = detail.Dataset,
            DefinitionDate = ValueDate,
            ProviderContractId = detail.RawSymbol,
            Ticker = detail.Ticker,
            LocalSymbol = detail.RawSymbol,
            SecurityType = detail.SecurityType,
            Currency = detail.Currency,
            Exchange = detail.Exchange,
            ContractMultiplier = detail.ContractMultiplier ?? 1,
            MaturityDate = detail.MaturityDate ?? ValueDate,
            IsCurrentlyTraded = resolved.Futures?.CurrentlyTraded ?? true,
            StrikePrice = detail.StrikePrice is { } strike
                ? strike / 1_000_000_000m
                : null,
            OptionType = detail.ContractKind switch
            {
                ContractKind.CallOption => "Call",
                ContractKind.PutOption => "Put",
                _ => null
            },
            UnderlyingContractId = resolved.Registration.AssetTypeId == AssetTypeId.FuturesOption
                ? _catalog!.FindOptionUnderlying(resolved.Registration.DomainContractId)
                : null
        };
    }

    private static void ValidateRuntimeOptions(DatabentoMarketDataRuntimeOptions options)
    {
        if (options.QueryConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.QueryConcurrency));
        if (options.QueryQueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.QueryQueueCapacity));
        if (options.LastPriceCapacity < options.Contracts.Count)
            throw new ArgumentOutOfRangeException(nameof(options.LastPriceCapacity));
        if (options.MaximumConcurrentOptionChains <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumConcurrentOptionChains));
    }

    private sealed class EpochValueDateProvider(DateOnly valueDate) : ITickValueDateProvider
    {
        public DateOnly GetValueDate(DateTime timestampUtc) => valueDate;
    }
}
