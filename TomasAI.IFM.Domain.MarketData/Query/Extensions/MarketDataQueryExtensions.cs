#pragma warning disable CA1034 // C# 14 extension blocks generate nested metadata types; no public nested type is declared here.
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
public static partial class MarketDataQueryExtensions
{
    extension(IMarketDataQueryContext context)
    {
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets currently traded futures contract.
        /// </summary>
        /// <param name="symbol">The market symbol.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<FuturesContractV3ReadModel>> GetOnTheRunFuturesContractAsync(
            string symbol)
        {
            try
            {
                FuturesContractV3ReadModel result =
                    (await (context.DbFactory.SecuritiesDb.GetOnTheRunFuturesContractAsync(symbol)).ConfigureAwait(false))!;
                return new ServiceOk<FuturesContractV3ReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FuturesContractV3ReadModel>(
                    GetOnTheRunFuturesContractQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets currently traded futures contracts.
        /// </summary>
        /// <param name="symbol">The market symbol.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<FuturesContractV3ReadModel[]>> GetRolloverFuturesContractsAsync(
            string symbol)
        {
            try
            {
                FuturesContractV3ReadModel[] result =
                    [.. await (context.DbFactory.SecuritiesDb.GetRolloverFuturesContractsAsync(symbol)).ConfigureAwait(false)];
                return new ServiceOk<FuturesContractV3ReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FuturesContractV3ReadModel[]>(
                    GetRolloverFuturesContractsQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets futures contract.
        /// </summary>
        /// <param name="contractId">The contract identifier.</param>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<FuturesContractV3ReadModel>> GetFuturesContractAsync(string contractId)
        {
            try
            {
                FuturesContractV3ReadModel result =
                    (await (context.DbFactory.SecuritiesDb.GetFuturesContractAsync(contractId)).ConfigureAwait(false))!;
                return new ServiceOk<FuturesContractV3ReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FuturesContractV3ReadModel>(GetFuturesContractQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                    (await (context.DbFactory.SecuritiesDb.GetFuturesContractAsync(contractId)).ConfigureAwait(false))?.Symbol ?? string.Empty;
                return new ServiceOk<string>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<string>(GetFuturesContractSymbolQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                    (await (context.DbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId)).ConfigureAwait(false))!;
                return new ServiceOk<FuturesOptionContractReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FuturesOptionContractReadModel>(
                    GetFuturesOptionContractQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets futures contracts.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<FuturesContractV3ReadModel[]>> GetFuturesContractsAsync()
        {
            try
            {
                FuturesContractV3ReadModel[] result =
                    [.. await (context.DbFactory.SecuritiesDb.GetFuturesContractsAsync()).ConfigureAwait(false)];
                return new ServiceOk<FuturesContractV3ReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FuturesContractV3ReadModel[]>(GetFuturesContractsQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                    [.. await (context.DbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)).ConfigureAwait(false)];
                return new ServiceOk<FuturesOptionContractReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FuturesOptionContractReadModel[]>(
                    GetFuturesOptionContractsQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                var contracts = await (context.DbFactory.SecuritiesDb
                    .GetFuturesOptionContractsByIdsAsync(uniqueContractIds)).ConfigureAwait(false);
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets last yield curve rate.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
        {
            try
            {
                YieldCurveRateReadModel result =
                    (await (context.DbFactory.MarketDataDb.GetLastYieldCurveRateAsync()).ConfigureAwait(false))!;
                return new ServiceOk<YieldCurveRateReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<YieldCurveRateReadModel>(GetLastYieldCurveRateQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                RateOfReturnReadModel result = await (context.DbFactory.MarketDataDb.GetLastRateOfReturnAsync(symbol)).ConfigureAwait(false);
                return new ServiceOk<RateOfReturnReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<RateOfReturnReadModel>(GetLastRateOfReturnQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                var result = new ScalarReadModel<int>(await (context.DbFactory.MarketDataDb.GetTradingDayCountAsync(
                    startDate,
                    endDate,
                    marketType,
                    currencyType)).ConfigureAwait(false));
                return new ServiceOk<ScalarReadModel<int>>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<ScalarReadModel<int>>(GetTradingDaysQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                DateOnly[] result = await (context.DbFactory.MarketDataDb.GetTradingDatesAsync(
                    startDate,
                    endDate,
                    marketType,
                    currencyType)).ConfigureAwait(false);
                return new ServiceOk<DateOnly[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<DateOnly[]>(GetTradingDatesQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                    [.. await (context.DbFactory.MarketDataDb.GetYieldCurveRatesAsync(startDate, endDate)).ConfigureAwait(false)];
                return new ServiceOk<YieldCurveRateReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<YieldCurveRateReadModel[]>(GetYieldCurveRatesQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets yield curve rate years.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
        {
            try
            {
                var result = new YieldCurveRateYearsReadModel(
                    [.. await (context.DbFactory.MarketDataDb.GetYieldCurveRateYearsAsync()).ConfigureAwait(false)]);
                return new ServiceOk<YieldCurveRateYearsReadModel>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<YieldCurveRateYearsReadModel>(
                    GetYieldCurveRateYearsQuery.ErrorId,
                    ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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
                    await (context.DbFactory.MarketDataDb.GetYieldCurveRateExistsAsync(valueDate)).ConfigureAwait(false));
                return new ServiceOk<ScalarReadModel<bool>>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<ScalarReadModel<bool>>(GetYieldCurveRateExistsQuery.ErrorId, ex.Message);
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
        /// <summary>
        /// Gets value date.
        /// </summary>
        /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
        public Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
        {
            try
            {
                var result = new ScalarReadModel<DateOnly>(
                    context.MarketSessionAuthority.Current.OperationalValueDate);
                return Task.FromResult<ServiceResult<ScalarReadModel<DateOnly>>>(
                    new ServiceOk<ScalarReadModel<DateOnly>>(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult<ServiceResult<ScalarReadModel<DateOnly>>>(
                    new ServiceFailed<ScalarReadModel<DateOnly>>(GetValueDateQuery.ErrorId, ex.Message));
            }
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
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

                await (Task.WhenAll(
                    underlyingTask, optionsTask, yieldCurveTask, tradingDayCountTask)).ConfigureAwait(false);

                var underlying = await (underlyingTask).ConfigureAwait(false);
                var options = await (optionsTask).ConfigureAwait(false);
                var yieldCurve = await (yieldCurveTask).ConfigureAwait(false);
                var tradingDayCount = await (tradingDayCountTask).ConfigureAwait(false);
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

#pragma warning restore CA1034
