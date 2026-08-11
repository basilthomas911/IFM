using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Date-scoped application orchestration over the DataBento framework
/// services. Provider symbols and instrument IDs remain inside the epoch.
/// </summary>
public sealed class DatabentoMarketDataApi : IMarketDataSnapshotApi, IAsyncDisposable
{
    private readonly IDatabentoMarketDataEpochFactory _epochFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _maximumLastPriceAge;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private IDatabentoMarketDataEpoch? _epoch;
    private Func<Guid, int, string, Task>? _errorMessageHandler;

    public DateOnly? ActiveValueDate => Volatile.Read(ref _epoch)?.ValueDate;

    public DatabentoMarketDataApiHealth GetHealth()
    {
        var active = Volatile.Read(ref _epoch);
        return active is null
            ? new DatabentoMarketDataApiHealth(false, null, null)
            : new DatabentoMarketDataApiHealth(
                true, active.ValueDate, active.GetHealth());
    }

    public DatabentoMarketDataApi(
        IDatabentoMarketDataEpochFactory epochFactory,
        DatabentoMarketDataApiOptions options,
        TimeProvider? timeProvider = null)
    {
        _epochFactory = epochFactory ?? throw new ArgumentNullException(nameof(epochFactory));
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumLastPriceAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumLastPriceAge));
        _maximumLastPriceAge = options.MaximumLastPriceAge;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task StartAsync(
        DateOnly valueDate,
        Func<Guid, int, string, Task>? errorMessageHandler = null,
        CancellationToken cancellationToken = default)
    {
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

    public async Task StopAsync(DateOnly valueDate)
    {
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

    public Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        string futuresContractId)
    {
        var active = GetRunningEpoch();
        ValidateContractId(futuresContractId, nameof(futuresContractId));
        if (active.Catalog.FindFuturesOption(futuresContractId) is not null)
            throw KindMismatch(futuresContractId, "futures", "futures option");
        return Task.FromResult(active.Catalog.FindFutures(futuresContractId));
    }

    public async Task<FuturesContractV2ReadModel[]> GetFuturesContractsAsync(
        string[] futuresContractIds)
    {
        ArgumentNullException.ThrowIfNull(futuresContractIds);
        GetRunningEpoch();
        if (futuresContractIds.Length == 0) return [];

        var results = new FuturesContractV2ReadModel[futuresContractIds.Length];
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

    public Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        string futuresOptionContractId)
    {
        var active = GetRunningEpoch();
        ValidateContractId(futuresOptionContractId, nameof(futuresOptionContractId));
        if (active.Catalog.FindFutures(futuresOptionContractId) is not null)
            throw KindMismatch(futuresOptionContractId, "futures option", "futures");
        return Task.FromResult(active.Catalog.FindFuturesOption(futuresOptionContractId));
    }

    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
        string[] futuresOptionContractIds)
    {
        ArgumentNullException.ThrowIfNull(futuresOptionContractIds);
        GetRunningEpoch();
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

    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        ValidateDate(maturityDate, nameof(maturityDate));
        return active.Catalog.GetOptionChainAsync(futuresContractId, maturityDate);
    }

    public Task<decimal> GetFuturesPriceAsync(string futuresContractId)
    {
        var reader = GetFuturesLastPriceReader(futuresContractId);
        if (!reader.TryGetLastTrade(out var trade)
            || trade.ContractId != futuresContractId
            || trade.ValueDate != reader.ValueDate
            || !IsFresh(trade.EventTimestamp))
        {
            throw new FuturesLastPriceUnavailableException(futuresContractId);
        }
        return Task.FromResult(trade.Price);
    }

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

    public IFuturesLastPriceReader GetFuturesLastPriceReader(string futuresContractId)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        return active.LastPrices.GetFuturesReader(futuresContractId, active.ValueDate);
    }

    public IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(
        string futuresOptionContractId)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        return active.LastPrices.GetFuturesOptionReader(
            futuresOptionContractId, active.ValueDate);
    }

    public Task<bool> StartStreamingFuturesTickDataAsync(string futuresContractId)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        return Task.FromResult(active.StartFuturesRoute(futuresContractId));
    }

    public Task<bool> StopStreamingFuturesTickDataAsync(string futuresContractId)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        return Task.FromResult(active.StopFuturesRoute(futuresContractId));
    }

    public Task<bool> StartStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        return Task.FromResult(active.StartIndividualOptionRoute(futuresOptionContractId));
    }

    public Task<bool> StopStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        return Task.FromResult(active.StopIndividualOptionRoute(futuresOptionContractId));
    }

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

    public Task<bool> StopStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        var active = GetRunningEpoch();
        RequireFutures(active, futuresContractId);
        ValidateDate(maturityDate, nameof(maturityDate));
        return active.StopOptionChainAsync(futuresContractId, maturityDate);
    }

    public async ValueTask DisposeAsync()
    {
        var active = Volatile.Read(ref _epoch);
        if (active is not null)
            await StopAsync(active.ValueDate).ConfigureAwait(false);
        _lifecycle.Dispose();
    }

    private IDatabentoMarketDataEpoch GetRunningEpoch() =>
        Volatile.Read(ref _epoch) ?? throw new MarketDataApiNotRunningException();

    private static FuturesContractV2ReadModel RequireFutures(
        IDatabentoMarketDataEpoch epoch,
        string contractId)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (epoch.Catalog.FindFutures(contractId) is { } contract) return contract;
        if (epoch.Catalog.FindFuturesOption(contractId) is not null)
            throw KindMismatch(contractId, "futures", "futures option");
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
