using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed class DatabentoMarketDataEpochFactory : IDatabentoMarketDataEpochFactory
{
    private readonly IDatabentoFeedFactory _feeds;
    private readonly ITickAggregationEventPublisher _publisher;
    private readonly DatabentoMarketDataRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ITickLiveEventPublisher _livePublisher;
    private readonly DatabentoTerminalFaultSignal? _terminalFaultSignal;
    private readonly ILoggerFactory? _loggerFactory;

    public DatabentoMarketDataEpochFactory(
        IDatabentoFeedFactory feeds,
        ITickAggregationEventPublisher publisher,
        DatabentoMarketDataRuntimeOptions options,
        TimeProvider? timeProvider = null,
        ITickLiveEventPublisher? livePublisher = null,
        DatabentoTerminalFaultSignal? terminalFaultSignal = null,
        ILoggerFactory? loggerFactory = null)
    {
        _feeds = feeds ?? throw new ArgumentNullException(nameof(feeds));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _livePublisher = livePublisher ?? new NullTickLiveEventPublisher();
        _terminalFaultSignal = terminalFaultSignal;
        _loggerFactory = loggerFactory;
    }

    public IDatabentoMarketDataEpoch Create(DateOnly valueDate)
    {
        var contracts = _options.Contracts is IDatabentoContractRegistrationRegistry registry
            ? registry.Snapshot()
            : _options.Contracts.ToArray();
        var snapshot = _options with { Contracts = contracts };
        return
        new DatabentoMarketDataEpoch(
            valueDate, _feeds, _publisher, snapshot, _timeProvider, _livePublisher,
            _terminalFaultSignal is null ? null : detail => _terminalFaultSignal.Notify(detail),
            _loggerFactory);
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
    private readonly Action<string>? _terminalFaultHandler;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly List<DatabentoOperationRunner> _operations = [];
    private DatabentoMarketDataCatalog? _catalog;
    private DatabentoLastPriceStore? _lastPrices;
    private DatabentoTickContractMappingStore? _mappings;
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
        ITickLiveEventPublisher livePublisher,
        Action<string>? terminalFaultHandler = null,
        ILoggerFactory? loggerFactory = null)
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
        _terminalFaultHandler = terminalFaultHandler;
        _loggerFactory = loggerFactory;
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

    public bool TryGetFuturesSessionStatistics(
        string contractId,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (!Volatile.Read(ref _aggregationByContractId)
            .TryGetValue(contractId, out var aggregation))
        {
            snapshot = default;
            return false;
        }
        return aggregation.TryGetFuturesSessionStatistics(contractId, out snapshot);
    }

    public bool IsTickDataStreamActive(string contractId) =>
        Volatile.Read(ref _aggregationByContractId)
            .GetValueOrDefault(contractId)?.IsTickDataStreamActive(contractId) == true;

    public bool IsFeedUp(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || Volatile.Read(ref _started) == 0)
            return false;

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var aggregations = Volatile.Read(ref _aggregationsByDataset).Values.ToArray();
            if (aggregations.Length == 0)
                return false;
            foreach (var aggregation in aggregations)
            {
                if (Stopwatch.GetElapsedTime(startedAt) > timeout
                    || !aggregation.IsFeedUp())
                    return false;
            }
            return Volatile.Read(ref _started) != 0
                && Stopwatch.GetElapsedTime(startedAt) <= timeout;
        }
        catch
        {
            return false;
        }
    }

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
            _mappings = mappings;
            var contractsByDataset = _catalog.ResolvedContracts
                .GroupBy(static resolved => resolved.Dataset, StringComparer.Ordinal)
                .Select(static group => (Dataset: group.Key, Contracts: group.ToArray()))
                .OrderBy(static group => DatabentoDatasetSelection.StartupPriority(group.Dataset))
                .ThenBy(static group => group.Dataset, StringComparer.Ordinal)
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
                        _options.FeedOptions with
                        {
                            Dataset = dataset,
                            StatisticsReplayStartTimestampNanoseconds =
                                ToUnixNanoseconds(
                                    FuturesTradingValueDate.GetSessionStartUtc(ValueDate)),
                            TradeReplayStartTimestampNanoseconds =
                                ToUnixNanoseconds(
                                    FuturesTradingValueDate.GetSessionStartUtc(ValueDate))
                        });
                    TickAggregationService? aggregation = null;
                    try
                    {
                        var subscriptions = contracts.Select(resolved => new TickerSubscription(
                            resolved.Detail.RawSymbol,
                            DatabentoInputSymbology.RawSymbol,
                            GetTickerDataKinds(resolved)))
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
                                FeedStopTimeout = _options.FeedStopTimeout
                            },
                            _timeProvider,
                            _lastPrices,
                            _liveRouter,
                            _streamRoutes,
                            detail => _terminalFaultHandler?.Invoke($"{dataset}: {detail}"),
                            _loggerFactory?.CreateLogger<TickAggregationService>(),
                            Guid.CreateVersion7(_timeProvider.GetUtcNow()));
                        await aggregation.StartAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        try
                        {
                            if (aggregation is null) feed.Dispose();
                            else await aggregation.DisposeAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // Cleanup must not replace the provider/feed startup failure.
                        }
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
                {
                    try { await aggregation.DisposeAsync().ConfigureAwait(false); }
                    catch { /* Preserve the provider/feed startup failure. */ }
                }
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

    public async Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Dataset);
        if (request.TeardownTimeout <= TimeSpan.Zero
            || request.QualificationTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(request),
                "Dataset teardown and qualification timeouts must be positive.");
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureRunning();
            if (request.ValueDate != ValueDate)
                throw new InvalidOperationException(
                    $"Dataset reset value date {request.ValueDate:yyyy-MM-dd} does not match epoch {ValueDate:yyyy-MM-dd}.");

            var datasets = Volatile.Read(ref _aggregationsByDataset);
            if (!datasets.TryGetValue(request.Dataset, out var previous))
                throw new KeyNotFoundException($"Dataset '{request.Dataset}' is not active.");
            if (previous.GenerationId != request.ExpectedGenerationId)
                return new(request.Dataset, request.ExpectedGenerationId, previous.GenerationId, true,
                    "A newer dataset generation already owns the route; stale reset was ignored.");

            var contracts = _catalog!.ResolvedContracts.Where(contract =>
                    string.Equals(contract.Dataset, request.Dataset, StringComparison.Ordinal))
                .ToArray();
            var owners = previous.CaptureStreamOwners();

            try
            {
                using var teardown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                teardown.CancelAfter(request.TeardownTimeout);
                await previous.StopAsync(teardown.Token).ConfigureAwait(false);
                await previous.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                return new(request.Dataset, previous.GenerationId, Guid.Empty, false,
                    $"Dataset teardown did not complete: {exception.Message}");
            }

            // The old generation is now quiescent and can no longer accept work. Remove it
            // from API admission before clearing its dataset-scoped state and starting the
            // replacement generation.
            var remainingDatasets = datasets.ToDictionary(StringComparer.Ordinal);
            remainingDatasets.Remove(request.Dataset);
            var remainingContracts = Volatile.Read(ref _aggregationByContractId)
                .ToDictionary(StringComparer.Ordinal);
            foreach (var contract in contracts)
                remainingContracts.Remove(contract.Registration.DomainContractId);
            Volatile.Write(ref _aggregationsByDataset,
                remainingDatasets.ToFrozenDictionary(StringComparer.Ordinal));
            Volatile.Write(ref _aggregationByContractId,
                remainingContracts.ToFrozenDictionary(StringComparer.Ordinal));

            // A replacement generation starts without any price, session, sequence, buffer,
            // or aggregation history from the failed generation. Reader identities remain
            // stable for epoch consumers, but they report no value until the replacement
            // accepts a new observation.
            _lastPrices!.ResetContracts(contracts.Select(
                static contract => contract.Registration.DomainContractId));

            TickAggregationService? replacement = null;
            var generation = Guid.CreateVersion7(_timeProvider.GetUtcNow());
            try
            {
                using var qualification = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                qualification.CancelAfter(request.QualificationTimeout);
                replacement = await CreateDatasetAggregationAsync(
                    request.Dataset, generation, qualification.Token).ConfigureAwait(false);
                replacement.RestoreStreamOwners(owners);
                await QualifyReplacementAsync(replacement, qualification.Token)
                    .ConfigureAwait(false);

                var nextDatasets = remainingDatasets;
                nextDatasets[request.Dataset] = replacement;
                var nextContracts = remainingContracts;
                foreach (var contract in contracts)
                    nextContracts[contract.Registration.DomainContractId] = replacement;
                Volatile.Write(ref _aggregationsByDataset,
                    nextDatasets.ToFrozenDictionary(StringComparer.Ordinal));
                Volatile.Write(ref _aggregationByContractId,
                    nextContracts.ToFrozenDictionary(StringComparer.Ordinal));
                return new(request.Dataset, previous.GenerationId, generation, true,
                    "Dataset generation was torn down, replaced, and qualified.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                if (replacement is not null)
                {
                    try { await replacement.DisposeAsync().ConfigureAwait(false); }
                    catch { /* Preserve the reset qualification failure. */ }
                }
                return new(request.Dataset, previous.GenerationId, generation, false,
                    $"Replacement dataset did not qualify: {exception.Message}");
            }
        }
        finally { _lifecycle.Release(); }
    }

    async Task QualifyReplacementAsync(
        TickAggregationService replacement,
        CancellationToken cancellationToken)
    {
        const int observationCount = 3;
        var previous = replacement.GetFeedHealth();
        if (!replacement.IsFeedUp())
            throw new InvalidOperationException("Replacement feed did not qualify as operational.");

        var stalledObservations = 0;
        for (var observation = 1; observation < observationCount; observation++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), _timeProvider, cancellationToken)
                .ConfigureAwait(false);
            if (!replacement.IsFeedUp())
                throw new InvalidOperationException(
                    "Replacement feed stopped during data-path qualification.");

            var current = replacement.GetFeedHealth();
            var drainStalled = current.RingUsedRecords > 0
                && (current.DrainDiagnostics?.Stage == FeedDrainStage.WaitingForNativeSignal
                    || current.RecordsProduced > previous.RecordsProduced
                    && current.RecordsConsumed == previous.RecordsConsumed);
            stalledObservations = drainStalled ? stalledObservations + 1 : 0;
            if (stalledObservations >= 2)
            {
                throw new InvalidOperationException(
                    "Replacement feed produced records without native-drain progress.");
            }
            previous = current;
        }
    }

    async Task<TickAggregationService> CreateDatasetAggregationAsync(
        string dataset,
        Guid generation,
        CancellationToken cancellationToken)
    {
        var contracts = _catalog!.ResolvedContracts.Where(contract =>
                string.Equals(contract.Dataset, dataset, StringComparison.Ordinal))
            .ToArray();
        if (contracts.Length == 0)
            throw new InvalidOperationException($"Dataset '{dataset}' has no resolved contracts.");
        var feed = _feeds.CreateTickerFeed(_options.FeedOptions with
        {
            Dataset = dataset,
            StatisticsReplayStartTimestampNanoseconds = ToUnixNanoseconds(
                FuturesTradingValueDate.GetSessionStartUtc(ValueDate)),
            TradeReplayStartTimestampNanoseconds = ToUnixNanoseconds(
                FuturesTradingValueDate.GetSessionStartUtc(ValueDate))
        });
        TickAggregationService? aggregation = null;
        try
        {
            feed.Subscribe(contracts.Select(contract => new TickerSubscription(
                contract.Detail.RawSymbol,
                DatabentoInputSymbology.RawSymbol,
                GetTickerDataKinds(contract))).ToArray(), _options.ProviderQueryTimeout);
            aggregation = new TickAggregationService(
                feed,
                _mappings!,
                _publisher,
                new TickQuoteBufferPool(),
                new EpochValueDateProvider(ValueDate),
                new TickAggregationOptions
                {
                    Dataset = dataset,
                    DefinitionDate = ValueDate,
                    FeedStartTimeout = _options.FeedStartTimeout,
                    FeedStopTimeout = _options.FeedStopTimeout
                },
                _timeProvider,
                _lastPrices,
                _liveRouter,
                _streamRoutes,
                detail => _terminalFaultHandler?.Invoke($"{dataset}: {detail}"),
                _loggerFactory?.CreateLogger<TickAggregationService>(),
                generation);
            await aggregation.StartAsync(cancellationToken).ConfigureAwait(false);
            return aggregation;
        }
        catch
        {
            if (aggregation is null) feed.Dispose();
            else
            {
                try { await aggregation.DisposeAsync().ConfigureAwait(false); }
                catch { }
            }
            throw;
        }
    }

    public TickAggregationContractStatus GetAggregationStatus(string contractId) =>
        Volatile.Read(ref _aggregationByContractId).GetValueOrDefault(contractId)
            ?.GetContractStatus(contractId)
        ?? new TickAggregationContractStatus(
            contractId, AssetTypeId.Unknown, false, false, false);

    public DatabentoMarketDataEpochHealth GetHealth()
    {
        var aggregationsByDataset = Volatile.Read(ref _aggregationsByDataset);
        var aggregations = aggregationsByDataset.Values;
        var metrics = aggregations.Select(static aggregation => aggregation.GetMetrics()).ToArray();
        var datasetFeedStatuses = aggregationsByDataset
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new DatabentoDatasetFeedHealth(
                pair.Key,
                pair.Value.GenerationId,
                pair.Value.GetFeedHealth(),
                pair.Value.GetMetrics()))
            .ToArray();
        var statuses = Volatile.Read(ref _aggregationByContractId).Keys
            .Order(StringComparer.Ordinal)
            .Select(GetAggregationStatus)
            .ToArray();
        return new DatabentoMarketDataEpochHealth(
            ValueDate,
            Volatile.Read(ref _started) != 0,
            statuses.Length != 0 && statuses.All(static status => status.ContractRunning),
            _catalog?.ResolvedContracts.Count ?? 0,
            _lastPrices?.Count ?? 0,
            _lastPrices?.IsActive == true,
            metrics.Sum(static metric => metric.SourceQuoteRecords),
            metrics.Sum(static metric => metric.SourceTradeRecords),
            metrics.Sum(static metric => metric.PublicationFailures),
            metrics.Sum(static metric => metric.ProcessingFailures),
            statuses,
            datasetFeedStatuses);
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
            IsOnTheRun = resolved.Futures?.OnTheRun ?? true,
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

    private static MarketDataKinds GetTickerDataKinds(
        DatabentoMarketDataCatalog.ResolvedContract resolved)
    {
        var kinds = MarketDataKinds.Quote | MarketDataKinds.Trade;
        if (resolved.Registration.AssetTypeId == AssetTypeId.Futures)
            kinds |= MarketDataKinds.Statistics | MarketDataKinds.SessionVolume;
        return kinds;
    }

    private static ulong ToUnixNanoseconds(DateTimeOffset value) => checked(
        (ulong)(value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL);
}
