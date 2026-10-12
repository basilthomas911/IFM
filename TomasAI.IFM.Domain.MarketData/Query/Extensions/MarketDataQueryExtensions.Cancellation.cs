#pragma warning disable CA1034 // C# 14 extension blocks generate nested metadata types; no public nested type is declared here.
#pragma warning disable CA1708 // Extension-block metadata names are compiler generated; public methods have distinct signatures.
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query.Extensions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
public static partial class MarketDataQueryExtensions
{
    extension(IMarketDataQueryContext context)
    {
        public Task<ServiceResult<FuturesContractV3ReadModel>> GetOnTheRunFuturesContractAsync(
            string symbol,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetOnTheRunFuturesContractQuery.ErrorId, async () => (await (context.DbFactory.SecuritiesDb
                    .GetOnTheRunFuturesContractAsync(symbol, cancellationToken)).ConfigureAwait(false))!, cancellationToken);

        public Task<ServiceResult<FuturesContractV3ReadModel[]>> GetRolloverFuturesContractsAsync(
            string symbol,
            CancellationToken cancellationToken)
            => ExecuteAsync<FuturesContractV3ReadModel[]>(
                GetRolloverFuturesContractsQuery.ErrorId, async () => [.. await (context.DbFactory.SecuritiesDb
                .GetRolloverFuturesContractsAsync(symbol, cancellationToken)).ConfigureAwait(false)], cancellationToken);

        public Task<ServiceResult<FuturesContractV3ReadModel>> GetFuturesContractAsync(
            string contractId,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetFuturesContractQuery.ErrorId, async () => (await (context.DbFactory.SecuritiesDb
                    .GetFuturesContractAsync(contractId, cancellationToken)).ConfigureAwait(false))!, cancellationToken);

        public Task<ServiceResult<string>> GetFuturesContractSymbolAsync(
            string contractId,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetFuturesContractSymbolQuery.ErrorId, async () => (await (context.DbFactory.SecuritiesDb
                    .GetFuturesContractAsync(contractId, cancellationToken)).ConfigureAwait(false))?.Symbol ?? string.Empty, cancellationToken);

        public Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(
            string contractId,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetFuturesOptionContractQuery.ErrorId, async () => (await (context.DbFactory.SecuritiesDb
                    .GetFuturesOptionContractAsync(contractId, cancellationToken)).ConfigureAwait(false))!, cancellationToken);

        public Task<ServiceResult<FuturesContractV3ReadModel[]>> GetFuturesContractsAsync(
            CancellationToken cancellationToken)
            => ExecuteAsync<FuturesContractV3ReadModel[]>(
                GetFuturesContractsQuery.ErrorId, async () => [.. await (context.DbFactory.SecuritiesDb
                .GetFuturesContractsAsync(cancellationToken)).ConfigureAwait(false)], cancellationToken);

        public Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(
            string symbol,
            CancellationToken cancellationToken)
            => ExecuteAsync<FuturesOptionContractReadModel[]>(
                GetFuturesOptionContractsQuery.ErrorId, async () => [.. await (context.DbFactory.SecuritiesDb
                .GetFuturesOptionContractsAsync(symbol, cancellationToken)).ConfigureAwait(false)], cancellationToken);

        public Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(
            string[] contractIds,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetFuturesOptionContractIdsQuery.ErrorId, async () =>
                {
                    var uniqueContractIds = contractIds.Distinct(StringComparer.Ordinal).ToArray();
                    var contracts = await (context.DbFactory.SecuritiesDb
                        .GetFuturesOptionContractsByIdsAsync(uniqueContractIds, cancellationToken)).ConfigureAwait(false);
                    var existingContractIds = contracts
                        .Select(static contract => contract.ContractId)
                        .ToHashSet(StringComparer.Ordinal);
                    return contractIds.Where(existingContractIds.Contains).ToArray();
                }, cancellationToken);

        public Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync(
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetLastYieldCurveRateQuery.ErrorId, async () => (await (context.DbFactory.MarketDataDb
                    .GetLastYieldCurveRateAsync(cancellationToken)).ConfigureAwait(false))!, cancellationToken);

        public Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(
            string symbol,
            DateOnly valueDate,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetLastRateOfReturnQuery.ErrorId, async () => (await (context.DbFactory.MarketDataDb
                    .GetLastRateOfReturnAsync(symbol, cancellationToken)).ConfigureAwait(false))!, cancellationToken);

        public Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(
            DateOnly startDate,
            DateOnly endDate,
            MarketType marketType,
            CurrencyType currencyType,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetTradingDaysQuery.ErrorId, async () => new ScalarReadModel<int>(await (context.DbFactory.MarketDataDb
                    .GetTradingDayCountAsync(
                        startDate,
                        endDate,
                        marketType,
                        currencyType,
                        cancellationToken)).ConfigureAwait(false)), cancellationToken);

        public Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(
            DateOnly startDate,
            DateOnly endDate,
            MarketType marketType,
            CurrencyType currencyType,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetTradingDatesQuery.ErrorId, () => context.DbFactory.MarketDataDb.GetTradingDatesAsync(
                    startDate,
                    endDate,
                    marketType,
                    currencyType,
                    cancellationToken), cancellationToken);

        public Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken)
            => ExecuteAsync<YieldCurveRateReadModel[]>(
                GetYieldCurveRatesQuery.ErrorId, async () => [.. await (context.DbFactory.MarketDataDb
                .GetYieldCurveRatesAsync(startDate, endDate, cancellationToken)).ConfigureAwait(false)], cancellationToken);

        public Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync(
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetYieldCurveRateYearsQuery.ErrorId, async () => new YieldCurveRateYearsReadModel(
                    [.. await (context.DbFactory.MarketDataDb.GetYieldCurveRateYearsAsync(cancellationToken)).ConfigureAwait(false)]), cancellationToken);

        public Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(
            DateOnly valueDate,
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetYieldCurveRateExistsQuery.ErrorId, async () => new ScalarReadModel<bool>(await (context.DbFactory.MarketDataDb
                    .GetYieldCurveRateExistsAsync(valueDate, cancellationToken)).ConfigureAwait(false)), cancellationToken);

        public Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync(
            CancellationToken cancellationToken)
            => ExecuteAsync(
                GetValueDateQuery.ErrorId, () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(new ScalarReadModel<DateOnly>(
                        context.MarketSessionAuthority.Current.OperationalValueDate));
                }, cancellationToken);

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
                GetIronCondorMarketDataQuery.ErrorId, async () =>
                {
                    string[] optionContractIds =
                    [
                        shortPutOptionContractId,
                    longPutOptionContractId,
                    shortCallOptionContractId,
                    longCallOptionContractId
                    ];
                    var underlyingTask = context.DbFactory.SecuritiesDb
                        .GetFuturesContractAsync(underlyingContractId, cancellationToken);
                    var optionsTask = context.DbFactory.SecuritiesDb
                        .GetFuturesOptionContractsByIdsAsync(optionContractIds, cancellationToken);
                    var yieldCurveTask = context.DbFactory.MarketDataDb
                        .GetLastYieldCurveRateAsync(cancellationToken);
                    var tradingDayCountTask = context.DbFactory.MarketDataDb.GetTradingDayCountAsync(
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

                    var underlying = await (underlyingTask).ConfigureAwait(false);
                    var options = await (optionsTask).ConfigureAwait(false);
                    var yieldCurve = await (yieldCurveTask).ConfigureAwait(false);
                    var tradingDayCount = await (tradingDayCountTask).ConfigureAwait(false);
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
                }, cancellationToken);

    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
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

#pragma warning restore CA1708

#pragma warning restore CA1034
