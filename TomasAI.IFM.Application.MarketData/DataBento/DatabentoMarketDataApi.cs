using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Date-scoped application orchestration over the DataBento framework
/// services. Provider symbols and instrument IDs remain inside the epoch.
/// </summary>
public sealed class DatabentoMarketDataApi : IMarketDataApi, IAsyncDisposable
{
    readonly ITradeStrategySymbolCatalog? _symbolCatalog;
    public Task<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TradeStrategySymbolReadModel[]>> GetTradeStrategySymbolsAsync(
        TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType family, CancellationToken cancellationToken = default)
        => _symbolCatalog?.GetAsync(family, cancellationToken)
            ?? Task.FromResult<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TradeStrategySymbolReadModel[]>>(
                new TomasAI.IFM.Shared.EventSourcing.ServiceFailed<TradeStrategySymbolReadModel[]>(503, "Trade strategy symbol catalog is not configured."));
    private static readonly TimeSpan DefaultFeedUpTimeout = TimeSpan.FromSeconds(1);
    /// <inheritdoc />
    public bool TryGetOnTheRunFuturesContract(
        string symbol,
        out FuturesContractV3ReadModel contract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (_contractRegistry is not null)
            return _contractRegistry.TryGetOnTheRunFuturesContract(symbol, out contract);
        contract = default!;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetFuturesTermStructureContracts(
        string symbol,
        out FuturesTermStructureContracts contracts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (_contractRegistry is not null)
            return _contractRegistry.TryGetFuturesTermStructureContracts(symbol, out contracts);
        contracts = default;
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateFuturesTermStructureContractsAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ValidateDate(valueDate, nameof(valueDate));
        var resolver = _currentContractResolver;
        var registry = _contractRegistry;
        var store = _rolloverStore;
        if (resolver is null || store is null) return false;
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var rollover = await store.GetFuturesContractRolloverAsync(
            normalizedSymbol, cancellationToken).ConfigureAwait(false);
        if (rollover?.ContractId is { Length: > 0 }
            && rollover.NextRolloverDate is { } rolloverDate
            && valueDate < rolloverDate)
        {
            var persisted = (await store.GetFuturesRolloverSetAsync(
                    normalizedSymbol, cancellationToken).ConfigureAwait(false))
                .OrderBy(static contract => contract.LastTradeDate)
                .ToArray();
            if (persisted.Length == 2
                && persisted.All(static contract => contract.Rollover)
                && persisted.Count(static contract => contract.OnTheRun) == 1
                && persisted[0].OnTheRun
                && string.Equals(persisted[0].ContractId, rollover.ContractId, StringComparison.Ordinal))
            {
                registry?.ReplaceFuturesRolloverSet(normalizedSymbol, persisted);
                return false;
            }
        }

        var resolved = await resolver.ResolveEligibleAsync(
            normalizedSymbol, valueDate, 2, cancellationToken).ConfigureAwait(false);
        if (resolved.Count < 2) return false;
        var ordered = resolved
            .Where(contract => string.Equals(contract.Symbol, normalizedSymbol, StringComparison.Ordinal))
            .OrderBy(static contract => contract.LastTradeDate)
            .DistinctBy(static contract => contract.ContractId, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (ordered.Length != 2
            || ordered.Any(static contract => !contract.Rollover)
            || ordered.Count(static contract => contract.OnTheRun) != 1
            || !ordered[0].OnTheRun)
            return false;
        if (rollover is null)
        {
            throw new FuturesContractRolloverConfigurationException(
                $"The futures-contract rollover row for '{normalizedSymbol}' is missing.");
        }
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var replacement = rollover with
        {
            ContractId = ordered[0].ContractId,
            NextRolloverDate = ordered[0].LastTradeDate,
            UpdatedOn = now,
            UpdatedBy = nameof(DatabentoMarketDataApi)
        };
        await store.ReplaceFuturesRolloverSetAsync(
            replacement, ordered, cancellationToken).ConfigureAwait(false);
        registry?.ReplaceFuturesRolloverSet(normalizedSymbol, ordered);
        return true;
    }

    /// <summary>
    /// Reads the active epoch's normalized market-price hot-cache snapshot without checking stream ownership.
    /// </summary>
    public bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (_currentValues is not null)
            return _currentValues.TryGetLastTickPrice(contractId, out snapshot);
        var active = Volatile.Read(ref _epoch);
        if (active is null)
        {
            snapshot = default;
            return false;
        }
        return active.TryGetLastTickPrice(contractId, out snapshot);
    }

    /// <summary>
    /// Reads the latest futures-option snapshot without checking stream ownership.
    /// </summary>
    public bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (_currentValues is not null)
            throw new NotSupportedException("Option current-value readers require Stage 4 supervised integration.");
        var active = Volatile.Read(ref _epoch);
        if (active is null)
        {
            snapshot = default;
            return false;
        }
        return active.TryGetLastOptionTickPrice(contractId, out snapshot);
    }

    /// <summary>Reads the active epoch's complete session open/high/low snapshot.</summary>
    public bool TryGetFuturesSessionStatistics(
        string contractId,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (_currentValues is not null)
            return _currentValues.TryGetFuturesSessionStatistics(contractId, out snapshot);
        var active = Volatile.Read(ref _epoch);
        if (active is null)
        {
            snapshot = default;
            return false;
        }
        return active.TryGetFuturesSessionStatistics(contractId, out snapshot);
    }

    /// <summary>Returns whether at least one workflow owns the contract's live tick route.</summary>
    public bool IsTickDataStreamActive(string contractId)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (_currentValues is not null) return false; // Stage 3 has no transient workflow leases.
        return Volatile.Read(ref _epoch)?.IsTickDataStreamActive(contractId) == true;
    }

    private readonly IDatabentoMarketDataEpochFactory _epochFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _maximumLastPriceAge;
    private readonly IDatabentoCurrentFuturesContractResolver? _currentContractResolver;
    private readonly IFuturesContractRolloverStore? _rolloverStore;
    private readonly IDatabentoContractRegistrationRegistry? _contractRegistry;
    private readonly DatasetWorkerCurrentValues? _currentValues;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private IDatabentoMarketDataEpoch? _epoch;
    private Func<Guid, int, string, Task>? _errorMessageHandler;

    /// <summary>
    /// Gets the value date of the active market-data epoch, or <see langword="null"/>
    /// when the API is stopped.
    /// </summary>
    public DateOnly? ActiveValueDate => _currentValues is not null
        ? _currentValues.ActiveValueDate : Volatile.Read(ref _epoch)?.ValueDate;

    /// <summary>
    /// Returns a point-in-time health snapshot for the application API and its
    /// active DataBento epoch.
    /// </summary>
    /// <returns>
    /// A snapshot whose running flag is <see langword="false"/> when no epoch is active.
    /// </returns>
    public DatabentoMarketDataApiHealth GetHealth()
    {
        if (_currentValues is not null)
        {
            var status = _currentValues.GetStatus();
            return new DatabentoMarketDataApiHealth(status.IsRunning, status.ActiveValueDate, null);
        }
        var active = Volatile.Read(ref _epoch);
        return active is null
            ? new DatabentoMarketDataApiHealth(false, null, null)
            : new DatabentoMarketDataApiHealth(
                true, active.ValueDate, active.GetHealth());
    }

    public FuturesMarketHealthSnapshot GetFuturesMarketHealth(string contractId)
    {
        if(_currentValues is not null)return _currentValues.GetFuturesMarketHealth(contractId);
        var health=GetHealth();
        var epoch=health.Epoch;
        var healthy=epoch is {Running:true,ProcessingFailures:0,LastPriceStoreActive:true,LastPriceSlots:>0}&&TryGetLastTickPrice(contractId,out _);
        var generations=epoch?.DatasetFeedStatuses is { } statuses?string.Join("|",statuses.OrderBy(x=>x.Dataset,StringComparer.Ordinal).Select(x=>$"{x.Dataset}:{x.GenerationId:N}")):"legacy-epoch";
        return new(health.Running,healthy,generations,health.ValueDate,DateTimeOffset.UtcNow,epoch is { } e?Math.Max(e.SourceQuoteRecords,e.SourceTradeRecords):0);
    }

    /// <inheritdoc />
    public bool IsDatabentoFeedUp(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultFeedUpTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
            return false;
        if (_currentValues is not null) return _currentValues.IsFeedUp;
        try
        {
            return Volatile.Read(ref _epoch)?.IsFeedUp(effectiveTimeout) == true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public MarketDataFeedRuntimeStatusReadModel GetRuntimeStatus()
    {
        if (_currentValues is not null)
        {
            var status = _currentValues.GetStatus();
            return new MarketDataFeedRuntimeStatusReadModel
            {
                IsRunning = status.IsRunning,
                ActiveValueDate = status.ActiveValueDate,
                ObservedAtUtc = _timeProvider.GetUtcNow()
            };
        }
        var active = Volatile.Read(ref _epoch);
        return new MarketDataFeedRuntimeStatusReadModel
        {
            IsRunning = active is not null,
            ActiveValueDate = active?.ValueDate,
            ObservedAtUtc = _timeProvider.GetUtcNow()
        };
    }

    /// <summary>
    /// Initializes the application-level DataBento market-data API.
    /// </summary>
    /// <param name="epochFactory">Factory that creates one isolated runtime per value date.</param>
    /// <param name="options">Freshness and application API behavior settings.</param>
    /// <param name="timeProvider">
    /// Time source used for deterministic last-price freshness checks. The system
    /// provider is used when this argument is <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="epochFactory"/> or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the configured maximum last-price age is not positive.
    /// </exception>
    public DatabentoMarketDataApi(
        IDatabentoMarketDataEpochFactory epochFactory,
        DatabentoMarketDataApiOptions options,
        TimeProvider? timeProvider = null,
        IDatabentoCurrentFuturesContractResolver? currentContractResolver = null,
        IFuturesContractRolloverStore? rolloverStore = null,
        IDatabentoContractRegistrationRegistry? contractRegistry = null,
        DatasetWorkerCurrentValues? currentValues = null,
        ITradeStrategySymbolCatalog? symbolCatalog = null)
    {
        _epochFactory = epochFactory ?? throw new ArgumentNullException(nameof(epochFactory));
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumLastPriceAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumLastPriceAge));
        _maximumLastPriceAge = options.MaximumLastPriceAge;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _currentContractResolver = currentContractResolver;
        _rolloverStore = rolloverStore;
        _contractRegistry = contractRegistry;
        _currentValues = currentValues;
        _symbolCatalog = symbolCatalog;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateOnTheRunFuturesContractAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default,
        bool forceProviderRefresh = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ValidateDate(valueDate, nameof(valueDate));
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var store = _rolloverStore ?? throw new FuturesContractRolloverConfigurationException(
            "The futures-contract rollover store is not registered.");
        var resolver = _currentContractResolver ?? throw new FuturesContractRolloverConfigurationException(
            "The DataBento current-futures-contract resolver is not registered.");
        var existing = await store.GetFuturesContractRolloverAsync(
            normalizedSymbol, cancellationToken).ConfigureAwait(false)
            ?? throw new FuturesContractRolloverConfigurationException(
                $"The futures-contract rollover row for '{normalizedSymbol}' is missing.");

        if (!forceProviderRefresh
            && !string.IsNullOrWhiteSpace(existing.ContractId)
            && existing.NextRolloverDate is { } currentRolloverDate
            && valueDate < currentRolloverDate)
        {
            var persisted = await store.GetPersistedFuturesContractAsync(
                existing.ContractId,
                cancellationToken).ConfigureAwait(false);
            if (persisted is not null
                && persisted.OnTheRun
                && persisted.Rollover
                && string.Equals(
                    persisted.ContractId,
                    existing.ContractId,
                    StringComparison.Ordinal)
                && string.Equals(
                    persisted.Symbol,
                    normalizedSymbol,
                    StringComparison.Ordinal))
            {
                _contractRegistry?.ReplaceFuturesRolloverSet(normalizedSymbol, [persisted]);
                return false;
            }
        }

        var resolved = await resolver.ResolveAsync(
            normalizedSymbol, valueDate, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(resolved.Contract.Symbol, normalizedSymbol, StringComparison.Ordinal))
        {
            throw new MarketDataContractMappingException(
                resolved.Contract.ContractId,
                $"the resolved symbol '{resolved.Contract.Symbol}' does not match '{normalizedSymbol}'");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var replacement = existing with
        {
            ContractId = resolved.Contract.ContractId,
            NextRolloverDate = resolved.NextRolloverDate,
            UpdatedOn = now,
            UpdatedBy = nameof(DatabentoMarketDataApi)
        };
        await store.ReplaceOnTheRunFuturesContractAsync(
            replacement, resolved.Contract, cancellationToken).ConfigureAwait(false);
        _contractRegistry?.ReplaceFuturesRolloverSet(normalizedSymbol, [resolved.Contract]);
        return existing.NextRolloverDate != resolved.NextRolloverDate;
    }

    /// <summary>
    /// Creates and starts the DataBento runtime for a value date.
    /// Repeating the call for the active value date is idempotent.
    /// </summary>
    /// <param name="valueDate">Domain value date that scopes mappings, feeds, and readers.</param>
    /// <param name="errorMessageHandler">
    /// Optional best-effort callback notified when epoch startup fails. Callback
    /// failures never replace the original startup exception.
    /// </param>
    /// <param name="cancellationToken">Token that cancels startup and lifecycle admission.</param>
    /// <returns>A task that completes after the epoch is fully started.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="valueDate"/> is the default date.
    /// </exception>
    /// <exception cref="MarketDataApiAlreadyRunningException">
    /// Thrown when a different value date is already active.
    /// </exception>
    public async Task StartAsync(
        DateOnly valueDate,
        Func<Guid, int, string, Task>? errorMessageHandler = null,
        CancellationToken cancellationToken = default)
    {
        if (_currentValues is not null)
            throw new NotSupportedException("Supervised dataset lifecycle is owned by the dataset-worker lifecycle runtime.");
        ValidateDate(valueDate, nameof(valueDate));
        cancellationToken.ThrowIfCancellationRequested();
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_epoch is { } active)
            {
                if (active.ValueDate == valueDate) return;
                throw new MarketDataApiAlreadyRunningException(active.ValueDate, valueDate);
            }

            var candidate = _epochFactory.Create(valueDate);
            try
            {
                await candidate.StartAsync(cancellationToken).ConfigureAwait(false);
                _errorMessageHandler = errorMessageHandler;
                Volatile.Write(ref _epoch, candidate);
            }
            catch (Exception exception)
            {
                await RollbackStartAsync(candidate, exception).ConfigureAwait(false);
                await ReportErrorAsync(errorMessageHandler, 7101, exception).ConfigureAwait(false);
                throw;
            }
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>
    /// Stops and disposes the active epoch, invalidating its last-price readers.
    /// Calling this method when the API is already stopped is idempotent.
    /// </summary>
    /// <param name="valueDate">Value date of the epoch to stop.</param>
    /// <returns>A task that completes after feeds, workers, and epoch state are drained.</returns>
    /// <exception cref="MarketDataApiValueDateMismatchException">
    /// Thrown when <paramref name="valueDate"/> does not identify the active epoch.
    /// </exception>
    /// <exception cref="AggregateException">
    /// Thrown after cleanup when one or more stop or disposal operations fail.
    /// </exception>
    public async Task StopAsync(DateOnly valueDate)
    {
        if (_currentValues is not null)
            throw new NotSupportedException("Supervised dataset lifecycle is owned by the dataset-worker lifecycle runtime.");
        ValidateDate(valueDate, nameof(valueDate));
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_epoch is not { } active) return;
            if (active.ValueDate != valueDate)
                throw new MarketDataApiValueDateMismatchException(active.ValueDate, valueDate);

            List<Exception>? failures = null;
            try { await active.StopAsync().ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            try { await active.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
            Volatile.Write(ref _epoch, null);
            _errorMessageHandler = null;
            if (failures is not null)
                throw new AggregateException("The DataBento epoch did not stop cleanly.", failures);
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>Replaces exactly one failed dataset generation inside the active epoch.</summary>
    public async Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var active = GetRunningEpoch();
            return await active.ResetDatasetAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>
    /// Resolves one canonical domain futures contract from the active epoch catalog.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain futures contract identifier.</param>
    /// <returns>The resolved futures contract, or <see langword="null"/> when it is unknown.</returns>
    /// <exception cref="MarketDataContractKindMismatchException">
    /// Thrown when the identifier belongs to a futures option.
    /// </exception>
    public Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(
        string futuresContractId)
    {
        var catalog = GetRunningCatalog();
        ValidateContractId(futuresContractId, nameof(futuresContractId));
        if (catalog.FindFuturesOption(futuresContractId) is not null)
            throw KindMismatch(futuresContractId, "futures", "futures option");
        return Task.FromResult(catalog.FindFutures(futuresContractId));
    }

    /// <summary>
    /// Resolves a batch of canonical futures contract identifiers while preserving
    /// input order and duplicates.
    /// </summary>
    /// <param name="futuresContractIds">Futures contract identifiers to resolve.</param>
    /// <returns>An ordered array containing one resolved contract per input identifier.</returns>
    /// <exception cref="MarketDataBatchResolutionException">
    /// Thrown when any requested identifier cannot be resolved; no partial result is returned.
    /// </exception>
    public async Task<FuturesContractV3ReadModel[]> GetFuturesContractsAsync(
        string[] futuresContractIds)
    {
        ArgumentNullException.ThrowIfNull(futuresContractIds);
        GetRunningCatalog();
        if (futuresContractIds.Length == 0) return [];

        var results = new FuturesContractV3ReadModel[futuresContractIds.Length];
        var missing = new List<string>();
        for (var index = 0; index < futuresContractIds.Length; index++)
        {
            var result = await GetFuturesContractAsync(futuresContractIds[index])
                .ConfigureAwait(false);
            if (result is null) missing.Add(futuresContractIds[index]);
            else results[index] = result;
        }
        if (missing.Count != 0) throw new MarketDataBatchResolutionException(missing);
        return results;
    }

    /// <summary>
    /// Resolves one canonical domain futures-option contract from the active epoch catalog.
    /// </summary>
    /// <param name="futuresOptionContractId">Canonical domain futures-option contract identifier.</param>
    /// <returns>The resolved futures option, or <see langword="null"/> when it is unknown.</returns>
    /// <exception cref="MarketDataContractKindMismatchException">
    /// Thrown when the identifier belongs to an underlying futures contract.
    /// </exception>
    public Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        string futuresOptionContractId)
    {
        var catalog = GetRunningCatalog();
        ValidateContractId(futuresOptionContractId, nameof(futuresOptionContractId));
        if (catalog.FindFutures(futuresOptionContractId) is not null)
            throw KindMismatch(futuresOptionContractId, "futures option", "futures");
        return Task.FromResult(catalog.FindFuturesOption(futuresOptionContractId));
    }

    /// <summary>
    /// Resolves a batch of canonical futures-option identifiers while preserving
    /// input order and duplicates.
    /// </summary>
    /// <param name="futuresOptionContractIds">Futures-option identifiers to resolve.</param>
    /// <returns>An ordered array containing one resolved option per input identifier.</returns>
    /// <exception cref="MarketDataBatchResolutionException">
    /// Thrown when any requested identifier cannot be resolved; no partial result is returned.
    /// </exception>
    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
        string[] futuresOptionContractIds)
    {
        ArgumentNullException.ThrowIfNull(futuresOptionContractIds);
        GetRunningCatalog();
        if (futuresOptionContractIds.Length == 0) return [];

        var results = new FuturesOptionContractReadModel[futuresOptionContractIds.Length];
        var missing = new List<string>();
        for (var index = 0; index < futuresOptionContractIds.Length; index++)
        {
            var result = await GetFuturesOptionContractAsync(futuresOptionContractIds[index])
                .ConfigureAwait(false);
            if (result is null) missing.Add(futuresOptionContractIds[index]);
            else results[index] = result;
        }
        if (missing.Count != 0) throw new MarketDataBatchResolutionException(missing);
        return results;
    }

    /// <summary>
    /// Returns the complete configured futures-option chain for an underlying and maturity.
    /// This is a definition query and does not start live option-chain streaming.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain identifier of the underlying futures contract.</param>
    /// <param name="maturityDate">Exact option maturity to query.</param>
    /// <returns>A stably ordered array of matching option definitions; the array may be empty.</returns>
    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        var catalog = GetRunningCatalog();
        ValidateContractId(futuresContractId, nameof(futuresContractId));
        if (catalog.FindFutures(futuresContractId) is null)
        {
            if (catalog.FindFuturesOption(futuresContractId) is not null)
                throw KindMismatch(futuresContractId, "futures", "futures option");
            throw new MarketDataContractNotFoundException(futuresContractId);
        }
        ValidateDate(maturityDate, nameof(maturityDate));
        return catalog.GetOptionChainAsync(futuresContractId, maturityDate);
    }

    /// <summary>
    /// Gets the most recent usable futures price from the active aggregation
    /// epoch's in-memory last-price reader. A fresh trade is preferred; a fresh,
    /// valid two-sided quote midpoint is the fallback.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain futures contract identifier.</param>
    /// <returns>
    /// The latest accepted trade price or quote midpoint fallback; otherwise
    /// <see langword="null"/> when no current price is available.
    /// </returns>
    public Task<decimal?> GetFuturesPriceAsync(string futuresContractId)
    {
        var reader = GetFuturesLastPriceReader(futuresContractId);
        if (reader.TryGetLastTrade(out var trade)
            && trade.ContractId == futuresContractId
            && trade.ValueDate == reader.ValueDate
            && IsFresh(trade.EventTimestamp))
            return Task.FromResult<decimal?>(trade.Price);

        if (reader.TryGetLastQuote(out var quote)
            && quote.ContractId == futuresContractId
            && quote.ValueDate == reader.ValueDate
            && IsFresh(quote.EventTimestamp)
            && quote.TryGetMidpoint(out var midpoint))
            return Task.FromResult<decimal?>(midpoint);

        return Task.FromResult<decimal?>(null);
    }

    /// <summary>
    /// Gets the midpoint of the most recent fresh, valid two-sided futures-option quote.
    /// </summary>
    /// <param name="futuresOptionContractId">Canonical domain futures-option contract identifier.</param>
    /// <returns>
    /// The current bid/ask midpoint, or <see langword="null"/> when no fresh
    /// two-sided quote is available.
    /// </returns>
    /// <exception cref="InvalidFuturesOptionQuoteException">
    /// Thrown when a mapped quote has a bid greater than its ask.
    /// </exception>
    public Task<decimal?> GetFuturesOptionPriceAsync(string futuresOptionContractId)
    {
        var reader = GetFuturesOptionLastPriceReader(futuresOptionContractId);
        if (!reader.TryGetLastQuote(out var quote) || !IsFresh(quote.EventTimestamp))
            return Task.FromResult<decimal?>(null);
        if (quote.ContractId != futuresOptionContractId || quote.ValueDate != reader.ValueDate)
            throw new MarketDataContractMappingException(
                futuresOptionContractId, "the hot quote identity does not match its reader");
        if (quote.BidPrice is > 0m && quote.AskPrice is > 0m
            && quote.BidPrice > quote.AskPrice)
            throw new InvalidFuturesOptionQuoteException(
                futuresOptionContractId, "the bid exceeds the ask");
        return Task.FromResult(quote.TryGetMidpoint(out var midpoint)
            ? (decimal?)midpoint
            : null);
    }

    /// <summary>
    /// Returns the epoch-bound hot reader for a futures contract.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain futures contract identifier.</param>
    /// <returns>
    /// A stable reader that receives aggregation updates and reports misses after
    /// its owning epoch is stopped or replaced.
    /// </returns>
    public IFuturesLastPriceReader GetFuturesLastPriceReader(string futuresContractId)
    {
        if (_currentValues is not null) return _currentValues.GetFuturesReader(futuresContractId);
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        return active.LastPrices.GetFuturesReader(futuresContractId, active.ValueDate);
    }

    /// <summary>
    /// Returns the epoch-bound hot reader for a futures-option contract, including
    /// atomic quote/trade-with-Greeks read operations when enrichment is available.
    /// </summary>
    /// <param name="futuresOptionContractId">Canonical domain futures-option contract identifier.</param>
    /// <returns>
    /// A stable reader that receives aggregation updates and reports misses after
    /// its owning epoch is stopped or replaced.
    /// </returns>
    public IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(
        string futuresOptionContractId)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        return active.LastPrices.GetFuturesOptionReader(
            futuresOptionContractId, active.ValueDate);
    }

    /// <summary>
    /// Activates transient live quote/trade delivery for an underlying futures contract.
    /// Durable tick aggregation remains independently active.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain futures contract identifier.</param>
    /// <returns>
    /// <see langword="true"/> when the supplied owner was newly registered;
    /// otherwise <see langword="false"/> when that owner was already registered.
    /// </returns>
    public Task<bool> StartStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        return Task.FromResult(active.StartFuturesRoute(
            owner ?? CreateDefaultStreamOwner(active, "futures"),
            futuresContractId));
    }

    /// <summary>
    /// Deactivates transient live quote/trade delivery for an underlying futures
    /// contract without stopping its durable tick aggregation.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain futures contract identifier.</param>
    /// <returns>
    /// <see langword="true"/> when the supplied owner was removed; otherwise
    /// <see langword="false"/> when that owner was not registered.
    /// </returns>
    public Task<bool> StopStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        return Task.FromResult(active.StopFuturesRoute(
            owner ?? CreateDefaultStreamOwner(active, "futures"),
            futuresContractId));
    }

    /// <summary>
    /// Activates transient live quote/trade delivery for one futures-option contract.
    /// </summary>
    /// <param name="futuresOptionContractId">Canonical domain futures-option contract identifier.</param>
    /// <returns>
    /// <see langword="true"/> when the supplied owner was newly registered;
    /// otherwise <see langword="false"/> when that owner was already registered.
    /// </returns>
    /// <exception cref="MarketDataRouteConflictException">
    /// Thrown when an option-chain session already owns the option route.
    /// </exception>
    public Task<bool> StartStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        var futuresContractId = active.Catalog.FindOptionUnderlying(
            futuresOptionContractId);
        if (string.IsNullOrWhiteSpace(futuresContractId))
        {
            throw new MarketDataContractMappingException(
                futuresOptionContractId,
                "the option does not resolve to a configured underlying futures contract");
        }

        var status = active.GetAggregationStatus(futuresContractId);
        if (!status.ServiceRunning)
            throw new TickAggregationNotRunningException(futuresContractId);
        if (!status.ContractConfigured || !status.ContractRunning)
            throw new UnderlyingTickerNotRunningException(futuresContractId);
        return Task.FromResult(active.StartIndividualOptionRoute(
            owner ?? CreateDefaultStreamOwner(active, "option"),
            futuresOptionContractId));
    }

    /// <summary>
    /// Deactivates transient live delivery for one individually routed futures option.
    /// Durable multi-asset tick aggregation remains active.
    /// </summary>
    /// <param name="futuresOptionContractId">Canonical domain futures-option contract identifier.</param>
    /// <returns>
    /// <see langword="true"/> when an individual route was removed; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public Task<bool> StopStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        return Task.FromResult(active.StopIndividualOptionRoute(
            owner ?? CreateDefaultStreamOwner(active, "option"),
            futuresOptionContractId));
    }

    /// <summary>
    /// Starts one transient futures-option chain session after verifying that the
    /// corresponding underlying contract is configured and actively aggregating.
    /// The session does not persist option-chain events.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain identifier of the underlying futures contract.</param>
    /// <param name="maturityDate">Exact maturity shared by every requested option.</param>
    /// <param name="optionContractIds">
    /// Non-empty set of canonical option identifiers belonging to the underlying
    /// and maturity.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a new chain session is started; otherwise
    /// <see langword="false"/> when the identical session is already active.
    /// </returns>
    /// <exception cref="TickAggregationNotRunningException">
    /// Thrown when the multi-asset aggregation service is not running.
    /// </exception>
    /// <exception cref="UnderlyingTickerNotRunningException">
    /// Thrown when the requested underlying is not configured and running in aggregation.
    /// </exception>
    /// <exception cref="MarketDataContractMappingException">
    /// Thrown when an option does not belong to the requested underlying and maturity.
    /// </exception>
    /// <exception cref="MarketDataPricingInputUnavailableException">
    /// Thrown by the production epoch before provider-feed allocation until the
    /// required application-supplied risk-free rate is available.
    /// </exception>
    public Task<bool> StartStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        ValidateDate(maturityDate, nameof(maturityDate));
        ArgumentNullException.ThrowIfNull(optionContractIds);
        if (optionContractIds.Length == 0)
            throw new ArgumentException("At least one option contract is required.", nameof(optionContractIds));

        var status = active.GetAggregationStatus(futuresContractId);
        if (!status.ServiceRunning)
            throw new TickAggregationNotRunningException(futuresContractId);
        if (!status.ContractConfigured || !status.ContractRunning)
            throw new UnderlyingTickerNotRunningException(futuresContractId);
        foreach (var optionContractId in optionContractIds)
        {
            var option = RequireOption(active, optionContractId);
            if (active.Catalog.FindOptionUnderlying(optionContractId) != futuresContractId
                || option.ContractMonth != maturityDate)
            {
                throw new MarketDataContractMappingException(
                    optionContractId,
                    "the option does not belong to the requested underlying and maturity");
            }
        }
        return active.StartOptionChainAsync(
            futuresContractId, maturityDate, optionContractIds.ToArray());
    }

    /// <summary>
    /// Stops and drains a transient option-chain session and clears its live state.
    /// </summary>
    /// <param name="futuresContractId">Canonical domain identifier of the underlying futures contract.</param>
    /// <param name="maturityDate">Maturity that identifies the chain session.</param>
    /// <returns>
    /// <see langword="true"/> when an active session was stopped; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public Task<bool> StopStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        ValidateDate(maturityDate, nameof(maturityDate));
        return active.StopOptionChainAsync(futuresContractId, maturityDate);
    }

    /// <summary>
    /// Stops the active epoch, if any, and releases lifecycle synchronization resources.
    /// </summary>
    /// <returns>A value task that completes when owned runtime resources are released.</returns>
    public async ValueTask DisposeAsync()
    {
        var active = Volatile.Read(ref _epoch);
        if (active is not null)
            await StopAsync(active.ValueDate).ConfigureAwait(false);
        _lifecycle.Dispose();
    }

    private IDatabentoMarketDataEpoch GetRunningEpoch()
    {
        if (_currentValues is not null)
            throw new NotSupportedException("Transient subscription ownership and option readers require Stage 4 supervised integration; dataset lifecycle belongs to the worker runtime.");
        return Volatile.Read(ref _epoch) ?? throw new MarketDataApiNotRunningException();
    }

    private IDatabentoMarketDataCatalog GetRunningCatalog() =>
        _currentValues is not null ? _currentValues.GetCatalog() : GetRunningEpoch().Catalog;

    private static TickerStreamOwner CreateDefaultStreamOwner(
        IDatabentoMarketDataEpoch epoch,
        string legId) => new(
        nameof(DatabentoMarketDataApi),
        $"compatibility:{epoch.ValueDate:yyyy-MM-dd}",
        legId);

    private static FuturesContractV3ReadModel RequireFutures(
        IDatabentoMarketDataEpoch epoch,
        string contractId)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (epoch.Catalog.FindFutures(contractId) is { } contract) return contract;
        if (epoch.Catalog.FindFuturesOption(contractId) is not null)
            throw KindMismatch(contractId, "futures", "futures option");
        throw new MarketDataContractNotFoundException(contractId);
    }

    private static void RequireConfigured(
        IDatabentoMarketDataEpoch epoch,
        string contractId)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (epoch.Catalog.FindFutures(contractId) is not null
            || epoch.Catalog.FindFuturesOption(contractId) is not null)
            return;
        throw new MarketDataContractNotFoundException(contractId);
    }

    private static FuturesOptionContractReadModel RequireOption(
        IDatabentoMarketDataEpoch epoch,
        string contractId)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (epoch.Catalog.FindFuturesOption(contractId) is { } contract) return contract;
        if (epoch.Catalog.FindFutures(contractId) is not null)
            throw KindMismatch(contractId, "futures option", "futures");
        throw new MarketDataContractNotFoundException(contractId);
    }

    private bool IsFresh(DateTimeOffset timestamp)
    {
        var now = _timeProvider.GetUtcNow();
        return timestamp <= now && now - timestamp <= _maximumLastPriceAge;
    }

    private static MarketDataContractKindMismatchException KindMismatch(
        string contractId,
        string expected,
        string actual) => new(contractId, expected, actual);

    private static void ValidateDate(DateOnly value, string parameterName)
    {
        if (value == default) throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateContractId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A canonical domain contract ID is required.", parameterName);
    }

    private static async Task RollbackStartAsync(
        IDatabentoMarketDataEpoch candidate,
        Exception original)
    {
        try { await candidate.StopAsync().ConfigureAwait(false); }
        catch { /* Preserve the start failure. */ }
        try { await candidate.DisposeAsync().ConfigureAwait(false); }
        catch { /* Preserve the start failure. */ }
        _ = original;
    }

    private static async Task ReportErrorAsync(
        Func<Guid, int, string, Task>? handler,
        int code,
        Exception exception)
    {
        if (handler is null) return;
        try { await handler(Guid.NewGuid(), code, exception.Message).ConfigureAwait(false); }
        catch { /* Callback failure must not replace the operation failure. */ }
    }
}
