using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.UnitTests.Harness;

/// <summary>
/// Test-only executable model of the approved application semantics. The
/// production DataBento implementation can replace this SUT while retaining
/// the same fake provider controls and assertions.
/// </summary>
internal sealed class DeterministicMarketDataApi(
    FakeMarketDataEpochFactory epochFactory,
    TimeSpan maximumLastPriceAge) : IMarketDataApi
{
    public bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot)
    {
        var active = Volatile.Read(ref epoch);
        if (active is null)
        {
            snapshot = default;
            return false;
        }
        return active.TryGetLastTickPrice(contractId, out snapshot);
    }

    public bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot)
    {
        var active = Volatile.Read(ref epoch);
        if (active is null)
        {
            snapshot = default;
            return false;
        }
        return active.TryGetLastOptionTickPrice(contractId, out snapshot);
    }

    public bool IsTickDataStreamActive(string contractId) =>
        Volatile.Read(ref epoch)?.IsTickDataStreamActive(contractId) == true;

    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private FakeMarketDataEpoch? epoch;

    internal DateTimeOffset UtcNow { get; set; } =
        new(2026, 8, 10, 20, 0, 0, TimeSpan.Zero);
    internal FakeMarketDataEpoch? CurrentEpoch => Volatile.Read(ref epoch);

    public async Task StartAsync(
        DateOnly valueDate,
        Func<Guid, int, string, Task>? errorMessageHandler = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDate(valueDate, nameof(valueDate));
        cancellationToken.ThrowIfCancellationRequested();
        _ = errorMessageHandler;

        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (epoch is { } active)
            {
                if (active.ValueDate == valueDate)
                {
                    return;
                }
                throw new MarketDataApiAlreadyRunningException(active.ValueDate, valueDate);
            }

            var candidate = epochFactory.Create(valueDate);
            try
            {
                await candidate.StartAsync(cancellationToken);
                Volatile.Write(ref epoch, candidate);
            }
            catch
            {
                await candidate.StopAsync();
                await candidate.DisposeAsync();
                throw;
            }
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async Task StopAsync(DateOnly valueDate)
    {
        ValidateDate(valueDate, nameof(valueDate));
        await lifecycle.WaitAsync();
        try
        {
            if (epoch is not { } active)
            {
                return;
            }
            if (active.ValueDate != valueDate)
            {
                throw new MarketDataApiValueDateMismatchException(active.ValueDate, valueDate);
            }

            await active.StopAsync();
            await active.DisposeAsync();
            Volatile.Write(ref epoch, null);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        string futuresContractId)
    {
        var active = GetRunningEpoch();
        ValidateContractId(futuresContractId, nameof(futuresContractId));
        if (active.Catalog.Options.ContainsKey(futuresContractId))
        {
            throw KindMismatch(futuresContractId, "futures", "futures option");
        }
        return Task.FromResult(active.Catalog.FindFuture(futuresContractId));
    }

    public async Task<FuturesContractV2ReadModel[]> GetFuturesContractsAsync(
        string[] futuresContractIds)
    {
        ArgumentNullException.ThrowIfNull(futuresContractIds);
        if (futuresContractIds.Length == 0)
        {
            GetRunningEpoch();
            return [];
        }

        var results = new FuturesContractV2ReadModel[futuresContractIds.Length];
        var missing = new List<string>();
        for (var index = 0; index < futuresContractIds.Length; index++)
        {
            var result = await GetFuturesContractAsync(futuresContractIds[index]);
            if (result is null)
            {
                missing.Add(futuresContractIds[index]);
            }
            else
            {
                results[index] = result;
            }
        }
        if (missing.Count > 0)
        {
            throw new MarketDataBatchResolutionException(missing);
        }
        return results;
    }

    public Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        string futuresOptionContractId)
    {
        var active = GetRunningEpoch();
        ValidateContractId(futuresOptionContractId, nameof(futuresOptionContractId));
        if (active.Catalog.Futures.ContainsKey(futuresOptionContractId))
        {
            throw KindMismatch(futuresOptionContractId, "futures option", "futures");
        }
        return Task.FromResult(active.Catalog.FindOption(futuresOptionContractId));
    }

    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
        string[] futuresOptionContractIds)
    {
        ArgumentNullException.ThrowIfNull(futuresOptionContractIds);
        if (futuresOptionContractIds.Length == 0)
        {
            GetRunningEpoch();
            return [];
        }

        var results = new FuturesOptionContractReadModel[futuresOptionContractIds.Length];
        var missing = new List<string>();
        for (var index = 0; index < futuresOptionContractIds.Length; index++)
        {
            var result = await GetFuturesOptionContractAsync(futuresOptionContractIds[index]);
            if (result is null)
            {
                missing.Add(futuresOptionContractIds[index]);
            }
            else
            {
                results[index] = result;
            }
        }
        if (missing.Count > 0)
        {
            throw new MarketDataBatchResolutionException(missing);
        }
        return results;
    }

    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        var active = GetRunningEpoch();
        RequireFuture(active, futuresContractId);
        ValidateDate(maturityDate, nameof(maturityDate));
        Interlocked.Increment(ref active.Catalog.ProviderQueryCount);
        var results = active.Catalog.Options.Values
            .Where(option =>
                active.Catalog.OptionUnderlyings.GetValueOrDefault(option.ContractId)
                    == futuresContractId
                && option.ContractMonth == maturityDate)
            .OrderBy(option => option.StrikePrice)
            .ThenBy(option => option.OptionType, StringComparer.Ordinal)
            .ThenBy(option => option.ContractId, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(results);
    }

    public Task<decimal> GetFuturesPriceAsync(string futuresContractId)
    {
        var reader = GetFuturesLastPriceReader(futuresContractId);
        if (!reader.TryGetLastTrade(out var trade)
            || trade.ContractId != futuresContractId
            || trade.ValueDate != reader.ValueDate)
        {
            throw new FuturesLastPriceUnavailableException(futuresContractId);
        }
        if (!IsFresh(trade.EventTimestamp))
        {
            throw new FuturesLastPriceUnavailableException(futuresContractId);
        }
        return Task.FromResult(trade.Price);
    }

    public Task<decimal?> GetFuturesOptionPriceAsync(string futuresOptionContractId)
    {
        var reader = GetFuturesOptionLastPriceReader(futuresOptionContractId);
        if (!reader.TryGetLastQuote(out var quote) || !IsFresh(quote.EventTimestamp))
        {
            return Task.FromResult<decimal?>(null);
        }
        if (quote.ContractId != futuresOptionContractId || quote.ValueDate != reader.ValueDate)
        {
            throw new MarketDataContractMappingException(
                futuresOptionContractId,
                "the hot quote identity does not match its reader");
        }
        if (quote.BidPrice is > 0m && quote.AskPrice is > 0m
            && quote.BidPrice > quote.AskPrice)
        {
            throw new InvalidFuturesOptionQuoteException(
                futuresOptionContractId,
                "the bid exceeds the ask");
        }
        return Task.FromResult(
            quote.TryGetMidpoint(out var midpoint) ? (decimal?)midpoint : null);
    }

    public IFuturesLastPriceReader GetFuturesLastPriceReader(string futuresContractId)
    {
        var active = GetRunningEpoch();
        RequireFuture(active, futuresContractId);
        return active.GetFuturesReader(futuresContractId);
    }

    public IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(
        string futuresOptionContractId)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        return active.GetOptionReader(futuresOptionContractId);
    }

    public Task<bool> StartStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireFuture(active, futuresContractId);
        return Task.FromResult(active.StartFuturesRoute(
            owner ?? CreateDefaultOwner(active, "futures"),
            futuresContractId));
    }

    public Task<bool> StopStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireFuture(active, futuresContractId);
        return Task.FromResult(active.StopFuturesRoute(
            owner ?? CreateDefaultOwner(active, "futures"),
            futuresContractId));
    }

    public Task<bool> StartStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        var futuresContractId = active.Catalog.OptionUnderlyings.GetValueOrDefault(
            futuresOptionContractId);
        if (string.IsNullOrWhiteSpace(futuresContractId))
        {
            throw new MarketDataContractMappingException(
                futuresOptionContractId,
                "the option does not resolve to a configured underlying futures contract");
        }

        var status = active.TickAggregation.GetStatus(futuresContractId);
        if (!status.ServiceRunning)
            throw new TickAggregationNotRunningException(futuresContractId);
        if (!status.TickerConfigured || !status.TickerRunning)
            throw new UnderlyingTickerNotRunningException(futuresContractId);
        return Task.FromResult(active.StartIndividualOptionRoute(
            owner ?? CreateDefaultOwner(active, "option"),
            futuresOptionContractId));
    }

    public Task<bool> StopStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null)
    {
        var active = GetRunningEpoch();
        RequireOption(active, futuresOptionContractId);
        return Task.FromResult(active.StopIndividualOptionRoute(
            owner ?? CreateDefaultOwner(active, "option"),
            futuresOptionContractId));
    }

    public async Task<bool> StartStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds)
    {
        var active = GetRunningEpoch();
        RequireFuture(active, futuresContractId);
        ValidateDate(maturityDate, nameof(maturityDate));
        ArgumentNullException.ThrowIfNull(optionContractIds);
        if (optionContractIds.Length == 0)
        {
            throw new ArgumentException("At least one option contract is required.", nameof(optionContractIds));
        }
        var selectedOptions = optionContractIds.ToArray();

        var status = active.TickAggregation.GetStatus(futuresContractId);
        if (!status.ServiceRunning)
        {
            throw new TickAggregationNotRunningException(futuresContractId);
        }
        if (!status.TickerConfigured || !status.TickerRunning)
        {
            throw new UnderlyingTickerNotRunningException(futuresContractId);
        }

        foreach (var optionContractId in selectedOptions)
        {
            var option = RequireOption(active, optionContractId);
            if (active.Catalog.OptionUnderlyings.GetValueOrDefault(optionContractId)
                    != futuresContractId
                || option.ContractMonth != maturityDate)
            {
                throw new MarketDataContractMappingException(
                    optionContractId,
                    "the option does not belong to the requested underlying and maturity");
            }
        }

        _ = await active.TreasuryCurve.GetLatestAsync(active.ValueDate);
        return active.OptionRoutes.StartChain(
            futuresContractId,
            maturityDate,
            selectedOptions);
    }

    public Task<bool> StopStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        var active = GetRunningEpoch();
        RequireFuture(active, futuresContractId);
        ValidateDate(maturityDate, nameof(maturityDate));
        return Task.FromResult(active.OptionRoutes.StopChain(futuresContractId, maturityDate));
    }

    private FakeMarketDataEpoch GetRunningEpoch() =>
        Volatile.Read(ref epoch) ?? throw new MarketDataApiNotRunningException();

    private static TickerStreamOwner CreateDefaultOwner(
        FakeMarketDataEpoch active,
        string legId) => new(
        nameof(DeterministicMarketDataApi),
        $"compatibility:{active.ValueDate:yyyy-MM-dd}",
        legId);

    private static FuturesContractV2ReadModel RequireFuture(
        FakeMarketDataEpoch active,
        string contractId)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (active.Catalog.Futures.TryGetValue(contractId, out var future))
        {
            return future;
        }
        if (active.Catalog.Options.ContainsKey(contractId))
        {
            throw KindMismatch(contractId, "futures", "futures option");
        }
        throw new MarketDataContractNotFoundException(contractId);
    }

    private static FuturesOptionContractReadModel RequireOption(
        FakeMarketDataEpoch active,
        string contractId)
    {
        ValidateContractId(contractId, nameof(contractId));
        if (active.Catalog.Options.TryGetValue(contractId, out var option))
        {
            return option;
        }
        if (active.Catalog.Futures.ContainsKey(contractId))
        {
            throw KindMismatch(contractId, "futures option", "futures");
        }
        throw new MarketDataContractNotFoundException(contractId);
    }

    private static MarketDataContractKindMismatchException KindMismatch(
        string contractId,
        string expected,
        string actual) => new(contractId, expected, actual);

    private bool IsFresh(DateTimeOffset eventTimestamp) =>
        eventTimestamp <= UtcNow && UtcNow - eventTimestamp <= maximumLastPriceAge;

    private static void ValidateDate(DateOnly value, string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateContractId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A canonical domain contract ID is required.", parameterName);
        }
    }
}
