using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Query.Extensions;

/// <summary>
/// Provides direct, in-process Market Data queries without actor messaging.
/// </summary>
/// <remarks>
/// Futures and option contracts are read through the Securities store; market calendars, rates, and yield
/// curves are read through Market Data storage and the optional external yield-curve reader. Every public
/// query owns its typed success/failure mapping. The implementation does not capture actor context and may
/// be registered as a singleton.
/// </remarks>
public static partial class MarketDataQueryExtensions
{
    extension(IMarketDataQueryContext context)
    {

    /// <summary>
    /// Gets currently traded futures contract.
    /// </summary>
    /// <param name="symbol">The market symbol.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(
        string symbol)
    {
        try
        {
            FuturesContractV2ReadModel result =
                (await context.DbFactory.SecuritiesDb.GetCurrentlyTradedFuturesContractAsync(symbol))!;
            return new ServiceOk<FuturesContractV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel>(
                GetCurrentlyTradedFuturesContractQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets currently traded futures contracts.
    /// </summary>
    /// <param name="symbol">The market symbol.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(
        string symbol)
    {
        try
        {
            FuturesContractV2ReadModel[] result =
                [.. await context.DbFactory.SecuritiesDb.GetCurrentlyTradedFuturesContractsAsync(symbol)];
            return new ServiceOk<FuturesContractV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel[]>(
                GetCurrentlyTradedFuturesContractsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures contract.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId)
    {
        try
        {
            FuturesContractV2ReadModel result =
                (await context.DbFactory.SecuritiesDb.GetFuturesContractAsync(contractId))!;
            return new ServiceOk<FuturesContractV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel>(GetFuturesContractQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures contract symbol.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId)
    {
        try
        {
            string result =
                (await context.DbFactory.SecuritiesDb.GetFuturesContractAsync(contractId))?.Symbol ?? string.Empty;
            return new ServiceOk<string>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string>(GetFuturesContractSymbolQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures option contract.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(
        string contractId)
    {
        try
        {
            FuturesOptionContractReadModel result =
                (await context.DbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId))!;
            return new ServiceOk<FuturesOptionContractReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionContractReadModel>(
                GetFuturesOptionContractQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures contracts.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync()
    {
        try
        {
            FuturesContractV2ReadModel[] result =
                [.. await context.DbFactory.SecuritiesDb.GetFuturesContractsAsync()];
            return new ServiceOk<FuturesContractV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel[]>(GetFuturesContractsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets futures option contracts.
    /// </summary>
    /// <param name="symbol">The market symbol.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(
        string symbol)
    {
        try
        {
            FuturesOptionContractReadModel[] result =
                [.. await context.DbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)];
            return new ServiceOk<FuturesOptionContractReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionContractReadModel[]>(
                GetFuturesOptionContractsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures option contract IDs.
    /// </summary>
    /// <param name="contractIds">The contract identifiers to evaluate.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds)
    {
        try
        {
            var uniqueContractIds = contractIds.Distinct(StringComparer.Ordinal).ToArray();
            var contracts = await context.DbFactory.SecuritiesDb
                .GetFuturesOptionContractsByIdsAsync(uniqueContractIds);
            var existingContractIds = contracts
                .Select(static contract => contract.ContractId)
                .ToHashSet(StringComparer.Ordinal);
            string[] result = contractIds
                .Where(existingContractIds.Contains)
                .ToArray();
            return new ServiceOk<string[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string[]>(GetFuturesOptionContractIdsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets last yield curve rate.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
    {
        try
        {
            YieldCurveRateReadModel result =
                (await context.DbFactory.MarketDataDb.GetLastYieldCurveRateAsync())!;
            return new ServiceOk<YieldCurveRateReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<YieldCurveRateReadModel>(GetLastYieldCurveRateQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets last rate of return.
    /// </summary>
    /// <param name="symbol">The market symbol.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(
        string symbol, DateOnly valueDate)
    {
        try
        {
            RateOfReturnReadModel result = await context.DbFactory.MarketDataDb.GetLastRateOfReturnAsync(symbol);
            return new ServiceOk<RateOfReturnReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<RateOfReturnReadModel>(GetLastRateOfReturnQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trading days.
    /// </summary>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <param name="marketType">The market type.</param>
    /// <param name="currencyType">The market currency.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(
        DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        try
        {
            var result = new ScalarReadModel<int>(await context.DbFactory.MarketDataDb.GetTradingDayCountAsync(
                startDate,
                endDate,
                marketType,
                currencyType));
            return new ServiceOk<ScalarReadModel<int>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetTradingDaysQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trading dates.
    /// </summary>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <param name="marketType">The market type.</param>
    /// <param name="currencyType">The market currency.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(
        DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        try
        {
            DateOnly[] result = await context.DbFactory.MarketDataDb.GetTradingDatesAsync(
                startDate,
                endDate,
                marketType,
                currencyType);
            return new ServiceOk<DateOnly[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<DateOnly[]>(GetTradingDatesQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets yield curve rates.
    /// </summary>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(
        DateOnly startDate, DateOnly endDate)
    {
        try
        {
            YieldCurveRateReadModel[] result =
                [.. await context.DbFactory.MarketDataDb.GetYieldCurveRatesAsync(startDate, endDate)];
            return new ServiceOk<YieldCurveRateReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<YieldCurveRateReadModel[]>(GetYieldCurveRatesQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets yield curve rate years.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
    {
        try
        {
            var result = new YieldCurveRateYearsReadModel(
                [.. await context.DbFactory.MarketDataDb.GetYieldCurveRateYearsAsync()]);
            return new ServiceOk<YieldCurveRateYearsReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<YieldCurveRateYearsReadModel>(
                GetYieldCurveRateYearsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Determines whether yield curve rate exists.
    /// </summary>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate)
    {
        try
        {
            var result = new ScalarReadModel<bool>(
                await context.DbFactory.MarketDataDb.GetYieldCurveRateExistsAsync(valueDate));
            return new ServiceOk<ScalarReadModel<bool>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<bool>>(GetYieldCurveRateExistsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets value date.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
    {
        try
        {
            if (!FuturesTradingValueDate.TryGet(DateTime.Now, out var valueDate))
                throw new InvalidOperationException("The futures market weekend session is closed.");
            var result = new ScalarReadModel<DateOnly>(valueDate);
            return Task.FromResult<ServiceResult<ScalarReadModel<DateOnly>>>(
                new ServiceOk<ScalarReadModel<DateOnly>>(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ServiceResult<ScalarReadModel<DateOnly>>>(
                new ServiceFailed<ScalarReadModel<DateOnly>>(GetValueDateQuery.ErrorId, ex.Message));
        }
    }

    /// <summary>
    /// Gets iron condor market data.
    /// </summary>
    /// <param name="underlyingContractId">The underlying contract ID.</param>
    /// <param name="shortPutOptionContractId">The short put option contract ID.</param>
    /// <param name="longPutOptionContractId">The long put option contract ID.</param>
    /// <param name="shortCallOptionContractId">The short call option contract ID.</param>
    /// <param name="longCallOptionContractId">The long call option contract ID.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <param name="marketType">The market type.</param>
    /// <param name="currencyType">The market currency.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType)
    {
        try
        {
            string[] optionContractIds =
            [
                shortPutOptionContractId,
                longPutOptionContractId,
                shortCallOptionContractId,
                longCallOptionContractId
            ];
            var underlyingTask = context.DbFactory.SecuritiesDb
                .GetFuturesContractAsync(underlyingContractId);
            var optionsTask = context.DbFactory.SecuritiesDb
                .GetFuturesOptionContractsByIdsAsync(optionContractIds);
            var yieldCurveTask = context.DbFactory.MarketDataDb.GetLastYieldCurveRateAsync();
            var tradingDayCountTask = context.DbFactory.MarketDataDb.GetTradingDayCountAsync(
                startDate, endDate, marketType, currencyType);

            await Task.WhenAll(
                underlyingTask, optionsTask, yieldCurveTask, tradingDayCountTask);

            var underlying = await underlyingTask;
            var options = await optionsTask;
            var yieldCurve = await yieldCurveTask;
            var tradingDayCount = await tradingDayCountTask;
            var optionMap = options.ToDictionary(
                static option => option.ContractId,
                StringComparer.Ordinal);

            var result = new IronCondorMarketDataReadModel(
                underlying!,
                optionMap.GetValueOrDefault(shortPutOptionContractId)!,
                optionMap.GetValueOrDefault(longPutOptionContractId)!,
                optionMap.GetValueOrDefault(shortCallOptionContractId)!,
                optionMap.GetValueOrDefault(longCallOptionContractId)!,
                (yieldCurve?.OneMonth ?? 0) / 100,
                tradingDayCount);
            return new ServiceOk<IronCondorMarketDataReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<IronCondorMarketDataReadModel>(
                GetIronCondorMarketDataQuery.ErrorId,
                ex.Message);
        }
    }

    }
}

