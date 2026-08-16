using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query.Api;

public sealed partial class ActorMarketDataQueryApi
{
    public Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(
        string symbol,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetCurrentlyTradedFuturesContractQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.SecuritiesDb
                .GetCurrentlyTradedFuturesContractAsync(symbol, cancellationToken))!);

    public Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesContractV2ReadModel[]>(
            GetCurrentlyTradedFuturesContractsQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.SecuritiesDb
                .GetCurrentlyTradedFuturesContractsAsync(symbol, cancellationToken)]);

    public Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesContractQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.SecuritiesDb
                .GetFuturesContractAsync(contractId, cancellationToken))!);

    public Task<ServiceResult<string>> GetFuturesContractSymbolAsync(
        string contractId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesContractSymbolQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.SecuritiesDb
                .GetFuturesContractAsync(contractId, cancellationToken))?.Symbol ?? string.Empty);

    public Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(
        string contractId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesOptionContractQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.SecuritiesDb
                .GetFuturesOptionContractAsync(contractId, cancellationToken))!);

    public Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesContractV2ReadModel[]>(
            GetFuturesContractsQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.SecuritiesDb
                .GetFuturesContractsAsync(cancellationToken)]);

    public Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
        => ExecuteAsync<FuturesOptionContractReadModel[]>(
            GetFuturesOptionContractsQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.SecuritiesDb
                .GetFuturesOptionContractsAsync(symbol, cancellationToken)]);

    public Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(
        string[] contractIds,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetFuturesOptionContractIdsQuery.ErrorId,
            cancellationToken,
            async () =>
            {
                var uniqueContractIds = contractIds.Distinct(StringComparer.Ordinal).ToArray();
                var contracts = await _dbFactory.SecuritiesDb
                    .GetFuturesOptionContractsByIdsAsync(uniqueContractIds, cancellationToken);
                var existingContractIds = contracts
                    .Select(static contract => contract.ContractId)
                    .ToHashSet(StringComparer.Ordinal);
                return contractIds.Where(existingContractIds.Contains).ToArray();
            });

    public Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLastYieldCurveRateQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb
                .GetLastYieldCurveRateAsync(cancellationToken))!);

    public Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetLastRateOfReturnQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.MarketDataDb
                .GetLastRateOfReturnAsync(symbol, cancellationToken))!);

    public Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradingDaysQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<int>(await _dbFactory.MarketDataDb
                .GetTradingDayCountAsync(
                    startDate,
                    endDate,
                    marketType,
                    currencyType,
                    cancellationToken)));

    public Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradingDatesQuery.ErrorId,
            cancellationToken,
            () => _dbFactory.MarketDataDb.GetTradingDatesAsync(
                startDate,
                endDate,
                marketType,
                currencyType,
                cancellationToken));

    public Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => ExecuteAsync<YieldCurveRateReadModel[]>(
            GetYieldCurveRatesQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.MarketDataDb
                .GetYieldCurveRatesAsync(startDate, endDate, cancellationToken)]);

    public Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetYieldCurveRateYearsQuery.ErrorId,
            cancellationToken,
            async () => new YieldCurveRateYearsReadModel(
                [.. await _dbFactory.MarketDataDb.GetYieldCurveRateYearsAsync(cancellationToken)]));

    public Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetYieldCurveRateExistsQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<bool>(await _dbFactory.MarketDataDb
                .GetYieldCurveRateExistsAsync(valueDate, cancellationToken)));

    public Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetValueDateQuery.ErrorId,
            cancellationToken,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!FuturesTradingValueDate.TryGet(DateTime.Now, out var valueDate))
                    throw new InvalidOperationException("The futures market weekend session is closed.");
                return Task.FromResult(new ScalarReadModel<DateOnly>(valueDate));
            });

    public Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetIronCondorMarketDataQuery.ErrorId,
            cancellationToken,
            async () =>
            {
                string[] optionContractIds =
                [
                    shortPutOptionContractId,
                    longPutOptionContractId,
                    shortCallOptionContractId,
                    longCallOptionContractId
                ];
                var underlyingTask = _dbFactory.SecuritiesDb
                    .GetFuturesContractAsync(underlyingContractId, cancellationToken);
                var optionsTask = _dbFactory.SecuritiesDb
                    .GetFuturesOptionContractsByIdsAsync(optionContractIds, cancellationToken);
                var yieldCurveTask = _dbFactory.MarketDataDb
                    .GetLastYieldCurveRateAsync(cancellationToken);
                var tradingDayCountTask = _dbFactory.MarketDataDb.GetTradingDayCountAsync(
                    startDate,
                    endDate,
                    marketType,
                    currencyType,
                    cancellationToken);

                await Task.WhenAll(
                    underlyingTask,
                    optionsTask,
                    yieldCurveTask,
                    tradingDayCountTask).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                var underlying = await underlyingTask;
                var options = await optionsTask;
                var yieldCurve = await yieldCurveTask;
                var tradingDayCount = await tradingDayCountTask;
                var optionMap = options.ToDictionary(
                    static option => option.ContractId,
                    StringComparer.Ordinal);

                return new IronCondorMarketDataReadModel(
                    underlying!,
                    optionMap.GetValueOrDefault(shortPutOptionContractId)!,
                    optionMap.GetValueOrDefault(longPutOptionContractId)!,
                    optionMap.GetValueOrDefault(shortCallOptionContractId)!,
                    optionMap.GetValueOrDefault(longCallOptionContractId)!,
                    (yieldCurve?.OneMonth ?? 0) / 100,
                    tradingDayCount);
            });

    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        CancellationToken cancellationToken,
        Func<Task<T>> operation)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await operation().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceOk<T>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
