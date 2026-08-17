using System.Collections.Frozen;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
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

    public IDatabentoMarketDataEpoch Create(DateOnly valueDate)
    {
        var contracts = _options.Contracts is IDatabentoContractRegistrationRegistry registry
            ? registry.Snapshot()
            : _options.Contracts.ToArray();
        var snapshot = _options with { Contracts = contracts };
        return
        new DatabentoMarketDataEpoch(
            valueDate, _feeds, _publisher, snapshot, _timeProvider, _livePublisher);
    }
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
    private readonly ITickerStreamRouteController _streamRoutes;
    private readonly List<DatabentoOperationRunner> _operations = [];
    private DatabentoMarketDataCatalog? _catalog;
    private DatabentoLastPriceStore? _lastPrices;
    private FrozenDictionary<string, TickAggregationService> _aggregationsByDataset =
        FrozenDictionary<string, TickAggregationService>.Empty;
    private FrozenDictionary<string, TickAggregationService> _aggregationByContractId =
        FrozenDictionary<string, TickAggregationService>.Empty;
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
        _publisher = new ReferenceCountedTickAggregationEventPublisher(publisher);
        _options = options;
        _timeProvider = timeProvider;
        _optionRoutes = new DatabentoOptionRouteRegistry(options.MaximumConcurrentOptionChains);
        _liveRouter = new TickLiveRouter(livePublisher);
        _streamRoutes = new DatabentoTickerStreamRouteController(_liveRouter, _optionRoutes);
    }

    public DateOnly ValueDate { get; }
    public IDatabentoMarketDataCatalog Catalog =>
        _catalog ?? throw new InvalidOperationException("The epoch catalog is not ready.");
    public IDatabentoLastPriceReaderProvider LastPrices =>
        _lastPrices ?? throw new InvalidOperationException("The epoch last-price store is not ready.");
    /// <summary>
    /// Reads the latest normalized TickAggregation hot-cache snapshot without checking stream ownership.
    /// </summary>
    public bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!Volatile.Read(ref _aggregationByContractId)
            .TryGetValue(contractId, out var aggregation))
        {
            snapshot = default;
            return false;
        }
        return aggregation.TryGetLastTickPrice(contractId, out snapshot);
    }

    public bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!Volatile.Read(ref _aggregationByContractId)
            .TryGetValue(contractId, out var aggregation))
        {
            snapshot = default;
            return false;
        }
        return aggregation.TryGetLastOptionTickPrice(contractId, out snapshot);
    }

    public bool IsTickDataStreamActive(string contractId) =>
        Volatile.Read(ref _aggregationByContractId)
            .GetValueOrDefault(contractId)?.IsTickDataStreamActive(contractId) == true;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _started) != 0) return;
            ValidateRuntimeOptions(_options);
            var datasets = _options.Contracts
                .Select(registration => DatabentoDatasetSelection.Resolve(_options, registration))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var operationsByDataset = new Dictionary<string, IDatabentoOperationRunner>(
                StringComparer.Ordinal);
            foreach (var dataset in datasets)
            {
                var feedOptions = _options.FeedOptions with { Dataset = dataset };
                var queryClients = Enumerable.Range(0, _options.QueryConcurrency)
                    .Select(_ => _feeds.CreateMarketDataQueries(feedOptions))
                    .ToArray();
                var runner = new DatabentoOperationRunner(
                    queryClients, _options.QueryQueueCapacity);
                _operations.Add(runner);
                operationsByDataset.Add(dataset, runner);
            }
            _catalog = await DatabentoMarketDataCatalog.CreateAsync(
                operationsByDataset, _options, cancellationToken).ConfigureAwait(false);

            _lastPrices = new DatabentoLastPriceStore(
                ValueDate, _options.LastPriceCapacity);
            var mappings = new DatabentoTickContractMappingStore();
            var contractsByDataset = _catalog.ResolvedContracts
                .GroupBy(static resolved => resolved.Dataset, StringComparer.Ordinal)
                .Select(static group => (Dataset: group.Key, Contracts: group.ToArray()))
                .ToArray();
            foreach (var group in contractsByDataset)
            {
                for (var index = 0; index < group.Contracts.Length; index++)
                {
                    var resolved = group.Contracts[index];
                    var instrument = _options.FeedOptions.DataSource
                        == FeedDataSourceMode.Synthetic
                            ? new InstrumentKey(1, checked((uint)index + 1))
                            : resolved.Detail.Instrument;
                    mappings.SetTickMapping(
                        resolved.Dataset,
                        ValueDate,
                        instrument.PublisherId,
                        instrument.InstrumentId,
                        resolved.Registration.DomainContractId,
                        resolved.Registration.AssetTypeId,
                        CreateContractDetails(resolved));
                    _lastPrices.RegisterContract(
                        resolved.Registration.DomainContractId,
                        resolved.Registration.AssetTypeId);
                }
            }

            var aggregationsByDataset = new Dictionary<string, TickAggregationService>(
                StringComparer.Ordinal);
            var aggregationByContractId = new Dictionary<string, TickAggregationService>(
                StringComparer.Ordinal);
            try
            {
                foreach (var group in contractsByDataset)
                {
                    var dataset = group.Dataset;
                    var contracts = group.Contracts;
                    var feed = _feeds.CreateTickerFeed(
                        _options.FeedOptions with { Dataset = dataset });
                    TickAggregationService? aggregation = null;
                    try
                    {
                        var subscriptions = contracts.Select(resolved => new TickerSubscription(
                            resolved.Detail.RawSymbol,
                            DatabentoInputSymbology.RawSymbol,
                            MarketDataKinds.Quote | MarketDataKinds.Trade))
                            .ToArray();
                        feed.Subscribe(subscriptions, _options.ProviderQueryTimeout);
                        aggregation = new TickAggregationService(
                            feed,
                            mappings,
                            _publisher,
                            new TickQuoteBufferPool(),
                            new EpochValueDateProvider(ValueDate),
                            new TickAggregationOptions
                            {
                                Dataset = dataset,
                                DefinitionDate = ValueDate,
                                FeedStartTimeout = _options.FeedStartTimeout,
                                FeedStopTimeout = _options.FeedStopTimeout,
                                ReaderPollTimeout = _options.ReaderPollTimeout
                            },
                            _timeProvider,
                            _lastPrices,
                            _liveRouter,
                            _streamRoutes);
                        await aggregation.StartAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        if (aggregation is null) feed.Dispose();
                        else await aggregation.DisposeAsync().ConfigureAwait(false);
                        throw;
                    }
                    aggregationsByDataset.Add(dataset, aggregation);
                    foreach (var contract in contracts)
                        aggregationByContractId.Add(
                            contract.Registration.DomainContractId, aggregation);
                }
            }
            catch
            {
                foreach (var aggregation in aggregationsByDataset.Values)
                    await aggregation.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            Volatile.Write(ref _aggregationsByDataset,
                aggregationsByDataset.ToFrozenDictionary(StringComparer.Ordinal));
            Volatile.Write(ref _aggregationByContractId,
                aggregationByContractId.ToFrozenDictionary(StringComparer.Ordinal));

            Volatile.Write(ref _started, 1);
        }
        finally { _lifecycle.Release(); }
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            // Close epoch admission before beginning the potentially blocking
            // provider drain. Realtime work already queued by the feed can then
            // observe a typed stopped-epoch result instead of reacquiring routes.
            Volatile.Write(ref _started, 0);
            _optionRoutes.Clear();
            _liveRouter.Clear();
            var stopTasks = Volatile.Read(ref _aggregationsByDataset).Values
                .Select(static aggregation => Task.Run(async () =>
                {
                    try
                    {
                        await aggregation.StopAsync().ConfigureAwait(false);
                        return (Exception?)null;
                    }
                    catch (Exception exception)
                    {
                        return exception;
                    }
                }))
                .ToArray();
            var stopResults = await Task.WhenAll(stopTasks).ConfigureAwait(false);
            var failures = stopResults.Where(static exception => exception is not null)
                .Cast<Exception>()
                .ToList();
            _lastPrices?.Invalidate();
            if (failures.Count != 0)
                throw new AggregateException("The DataBento epoch stop failed.", failures);
        }
        finally { _lifecycle.Release(); }
    }

    public TickAggregationContractStatus GetAggregationStatus(string contractId) =>
        Volatile.Read(ref _aggregationByContractId).GetValueOrDefault(contractId)
            ?.GetContractStatus(contractId)
        ?? new TickAggregationContractStatus(
            contractId, AssetTypeId.Unknown, false, false, false);

    public DatabentoMarketDataEpochHealth GetHealth()
    {
        var aggregations = Volatile.Read(ref _aggregationsByDataset).Values;
        var metrics = aggregations.Select(static aggregation => aggregation.GetMetrics()).ToArray();
        return new DatabentoMarketDataEpochHealth(
            ValueDate,
            Volatile.Read(ref _started) != 0,
            aggregations.Any() && aggregations.All(static aggregation => aggregation.IsRunning),
            _catalog?.ResolvedContracts.Count ?? 0,
            _lastPrices?.Count ?? 0,
            _lastPrices?.IsActive == true,
            metrics.Sum(static metric => metric.SourceQuoteRecords),
            metrics.Sum(static metric => metric.SourceTradeRecords),
            metrics.Sum(static metric => metric.PublicationFailures));
    }

    public bool StartFuturesRoute(
        TickerStreamOwner owner,
        string futuresContractId)
    {
        EnsureRunning();
        return GetAggregation(futuresContractId).StartTickDataStream(owner, futuresContractId);
    }

    public bool StopFuturesRoute(
        TickerStreamOwner owner,
        string futuresContractId)
    {
        EnsureRunning();
        return GetAggregation(futuresContractId).StopTickDataStream(owner, futuresContractId);
    }

    public bool StartIndividualOptionRoute(
        TickerStreamOwner owner,
        string futuresOptionContractId)
    {
        EnsureRunning();
        return GetAggregation(futuresOptionContractId)
            .StartTickDataStream(owner, futuresOptionContractId);
    }

    public bool StopIndividualOptionRoute(
        TickerStreamOwner owner,
        string futuresOptionContractId)
    {
        EnsureRunning();
        return GetAggregation(futuresOptionContractId)
            .StopTickDataStream(owner, futuresOptionContractId);
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
        foreach (var aggregation in Volatile.Read(ref _aggregationsByDataset).Values)
            await aggregation.DisposeAsync().ConfigureAwait(false);
        foreach (var operations in _operations)
            await operations.DisposeAsync().ConfigureAwait(false);
        await _publisher.DisposeAsync().ConfigureAwait(false);
        _lastPrices?.Dispose();
        _lifecycle.Dispose();
    }

    private void EnsureRunning()
    {
        if (Volatile.Read(ref _started) == 0)
            throw new MarketDataApiNotRunningException();
    }

    private TickAggregationService GetAggregation(string contractId) =>
        Volatile.Read(ref _aggregationByContractId).GetValueOrDefault(contractId)
        ?? throw new MarketDataContractNotFoundException(contractId);

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
            Currency = resolved.Futures?.Currency
                ?? resolved.Option?.Currency
                ?? DatabentoContractMetadata.ResolveCurrency(
                    detail,
                    resolved.Registration.DomainContractId,
                    DatabentoContractMetadata.FindCurrencyFallback(
                        _options,
                        detail.Ticker)),
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
        public DateOnly GetValueDate(DateTime timestampUtc)
            => FuturesTradingValueDate.TryGet(
                    new DateTimeOffset(
                        DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc)),
                    out var resolved)
                ? resolved
                : valueDate;
    }
}
