using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Query.Api;

/// <summary>Provides direct, in-process Market Data queries without actor messaging.</summary>
public sealed class ActorMarketDataQueryApi(IDbContextFactory dbFactory) : IActorMarketDataQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    public Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(string symbol)
        => ExecuteAsync(GetCurrentlyTradedFuturesContractQuery.ErrorId,
            async () => (await _dbFactory.SecuritiesDb.GetCurrentlyTradedFuturesContractAsync(symbol))!);

    public Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(string symbol)
        => ExecuteAsync<FuturesContractV2ReadModel[]>(GetCurrentlyTradedFuturesContractsQuery.ErrorId,
            async () => [.. await _dbFactory.SecuritiesDb.GetCurrentlyTradedFuturesContractsAsync(symbol)]);

    public Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId)
        => ExecuteAsync(GetFuturesContractQuery.ErrorId,
            async () => (await _dbFactory.SecuritiesDb.GetFuturesContractAsync(contractId))!);

    public Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId)
        => ExecuteAsync(GetFuturesContractSymbolQuery.ErrorId, async () =>
            (await _dbFactory.SecuritiesDb.GetFuturesContractAsync(contractId))?.Symbol ?? string.Empty);

    public Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId)
        => ExecuteAsync(GetFuturesOptionContractQuery.ErrorId,
            async () => (await _dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId))!);

    public Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync()
        => ExecuteAsync<FuturesContractV2ReadModel[]>(GetFuturesContractsQuery.ErrorId,
            async () => [.. await _dbFactory.SecuritiesDb.GetFuturesContractsAsync()]);

    public Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol)
        => ExecuteAsync<FuturesOptionContractReadModel[]>(GetFuturesOptionContractsQuery.ErrorId,
            async () => [.. await _dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)]);

    public Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds)
        => ExecuteAsync<string[]>(GetFuturesOptionContractIdsQuery.ErrorId, async () =>
        {
            List<string> existingContractIds = [];
            foreach (var contractId in contractIds)
            {
                if (await _dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId) is not null)
                    existingContractIds.Add(contractId);
            }
            return [.. existingContractIds];
        });

    public Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
        => ExecuteAsync(GetLastYieldCurveRateQuery.ErrorId,
            async () => (await _dbFactory.MarketDataDb.GetLastYieldCurveRateAsync())!);

    public Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(
        string symbol, DateOnly valueDate)
        => ExecuteAsync(GetLastRateOfReturnQuery.ErrorId,
            async () => await _dbFactory.MarketDataDb.GetLastRateOfReturnAsync(symbol));

    public Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(
        DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
        => ExecuteAsync(GetTradingDaysQuery.ErrorId, async () =>
            new ScalarReadModel<int>((await _dbFactory.MarketDataDb.GetTradingDatesAsync(
                startDate, endDate, marketType, currencyType)).Length));

    public Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(
        DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
        => ExecuteAsync(GetTradingDatesQuery.ErrorId,
            async () => await _dbFactory.MarketDataDb.GetTradingDatesAsync(
                startDate, endDate, marketType, currencyType));

    public Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(
        DateOnly startDate, DateOnly endDate)
        => ExecuteAsync<YieldCurveRateReadModel[]>(GetYieldCurveRatesQuery.ErrorId,
            async () => [.. await _dbFactory.MarketDataDb.GetYieldCurveRatesAsync(startDate, endDate)]);

    public Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
        => ExecuteAsync<YieldCurveRateReadModel[]>(GetExternalYieldCurveRatesQuery.ErrorId, async () =>
        {
            if (_dbFactory.YieldCurveRatesDb is not IYieldCurveRatesDbContext db)
                return [];
            return [.. await db.ReadAsync()];
        });

    public Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
        => ExecuteAsync(GetYieldCurveRateYearsQuery.ErrorId,
            async () => new YieldCurveRateYearsReadModel(
                [.. await _dbFactory.MarketDataDb.GetYieldCurveRateYearsAsync()]));

    public Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate)
        => ExecuteAsync(GetYieldCurveRateExistsQuery.ErrorId,
            async () => new ScalarReadModel<bool>(
                await _dbFactory.MarketDataDb.GetYieldCurveRateExistsAsync(valueDate)));

    public Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
        => ExecuteAsync(GetValueDateQuery.ErrorId,
            () => Task.FromResult(new ScalarReadModel<DateOnly>(CalculateValueDate(DateTime.Now))));

    public Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType)
        => ExecuteAsync(GetIronCondorMarketDataQuery.ErrorId, async () =>
        {
            var securitiesDb = _dbFactory.SecuritiesDb;
            var underlying = await securitiesDb.GetFuturesContractAsync(underlyingContractId);
            var shortPut = await securitiesDb.GetFuturesOptionContractAsync(shortPutOptionContractId);
            var longPut = await securitiesDb.GetFuturesOptionContractAsync(longPutOptionContractId);
            var shortCall = await securitiesDb.GetFuturesOptionContractAsync(shortCallOptionContractId);
            var longCall = await securitiesDb.GetFuturesOptionContractAsync(longCallOptionContractId);
            var yieldCurve = await _dbFactory.MarketDataDb.GetLastYieldCurveRateAsync();
            var tradingDates = await _dbFactory.MarketDataDb.GetTradingDatesAsync(
                startDate, endDate, marketType, currencyType);

            return new IronCondorMarketDataReadModel(
                underlying!, shortPut!, longPut!, shortCall!, longCall!,
                (yieldCurve?.OneMonth ?? 0) / 100,
                tradingDates.Length);
        });

    static DateOnly CalculateValueDate(DateTime today)
    {
        var valueDate = DateOnly.FromDateTime(today);
        if (today.DayOfWeek == DayOfWeek.Sunday && today.TimeOfDay >= TimeSpan.FromHours(18))
            return valueDate.AddDays(1);
        if (today.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday
            && today.TimeOfDay >= TimeSpan.FromHours(18))
            return valueDate.AddDays(1);
        return valueDate;
    }

    static async Task<ServiceResult<T>> ExecuteAsync<T>(int errorId, Func<Task<T>> query)
    {
        try
        {
            return new ServiceOk<T>(await query());
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
