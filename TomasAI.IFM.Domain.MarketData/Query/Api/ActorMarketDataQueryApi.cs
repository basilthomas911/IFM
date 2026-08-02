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

    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(
        string symbol)
    {
        try
        {
            FuturesContractV2ReadModel result =
                (await _dbFactory.SecuritiesDb.GetCurrentlyTradedFuturesContractAsync(symbol))!;
            return new ServiceOk<FuturesContractV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel>(
                GetCurrentlyTradedFuturesContractQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(
        string symbol)
    {
        try
        {
            FuturesContractV2ReadModel[] result =
                [.. await _dbFactory.SecuritiesDb.GetCurrentlyTradedFuturesContractsAsync(symbol)];
            return new ServiceOk<FuturesContractV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel[]>(
                GetCurrentlyTradedFuturesContractsQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId)
    {
        try
        {
            FuturesContractV2ReadModel result =
                (await _dbFactory.SecuritiesDb.GetFuturesContractAsync(contractId))!;
            return new ServiceOk<FuturesContractV2ReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel>(GetFuturesContractQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId)
    {
        try
        {
            string result =
                (await _dbFactory.SecuritiesDb.GetFuturesContractAsync(contractId))?.Symbol ?? string.Empty;
            return new ServiceOk<string>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string>(GetFuturesContractSymbolQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(
        string contractId)
    {
        try
        {
            FuturesOptionContractReadModel result =
                (await _dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId))!;
            return new ServiceOk<FuturesOptionContractReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionContractReadModel>(
                GetFuturesOptionContractQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync()
    {
        try
        {
            FuturesContractV2ReadModel[] result =
                [.. await _dbFactory.SecuritiesDb.GetFuturesContractsAsync()];
            return new ServiceOk<FuturesContractV2ReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesContractV2ReadModel[]>(GetFuturesContractsQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(
        string symbol)
    {
        try
        {
            FuturesOptionContractReadModel[] result =
                [.. await _dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)];
            return new ServiceOk<FuturesOptionContractReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionContractReadModel[]>(
                GetFuturesOptionContractsQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds)
    {
        try
        {
            List<string> existingContractIds = [];
            foreach (var contractId in contractIds)
            {
                if (await _dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId) is not null)
                    existingContractIds.Add(contractId);
            }
            string[] result = [.. existingContractIds];
            return new ServiceOk<string[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string[]>(GetFuturesOptionContractIdsQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
    {
        try
        {
            YieldCurveRateReadModel result =
                (await _dbFactory.MarketDataDb.GetLastYieldCurveRateAsync())!;
            return new ServiceOk<YieldCurveRateReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<YieldCurveRateReadModel>(GetLastYieldCurveRateQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(
        string symbol, DateOnly valueDate)
    {
        try
        {
            RateOfReturnReadModel result = await _dbFactory.MarketDataDb.GetLastRateOfReturnAsync(symbol);
            return new ServiceOk<RateOfReturnReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<RateOfReturnReadModel>(GetLastRateOfReturnQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(
        DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        try
        {
            var result = new ScalarReadModel<int>((await _dbFactory.MarketDataDb.GetTradingDatesAsync(
                startDate,
                endDate,
                marketType,
                currencyType)).Length);
            return new ServiceOk<ScalarReadModel<int>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetTradingDaysQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(
        DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        try
        {
            DateOnly[] result = await _dbFactory.MarketDataDb.GetTradingDatesAsync(
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

    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(
        DateOnly startDate, DateOnly endDate)
    {
        try
        {
            YieldCurveRateReadModel[] result =
                [.. await _dbFactory.MarketDataDb.GetYieldCurveRatesAsync(startDate, endDate)];
            return new ServiceOk<YieldCurveRateReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<YieldCurveRateReadModel[]>(GetYieldCurveRatesQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
    {
        try
        {
            if (_dbFactory.YieldCurveRatesDb is not IYieldCurveRatesDbContext db)
                return new ServiceOk<YieldCurveRateReadModel[]>([]);
            YieldCurveRateReadModel[] result = [.. await db.ReadAsync()];
            return new ServiceOk<YieldCurveRateReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<YieldCurveRateReadModel[]>(
                GetExternalYieldCurveRatesQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
    {
        try
        {
            var result = new YieldCurveRateYearsReadModel(
                [.. await _dbFactory.MarketDataDb.GetYieldCurveRateYearsAsync()]);
            return new ServiceOk<YieldCurveRateYearsReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<YieldCurveRateYearsReadModel>(
                GetYieldCurveRateYearsQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate)
    {
        try
        {
            var result = new ScalarReadModel<bool>(
                await _dbFactory.MarketDataDb.GetYieldCurveRateExistsAsync(valueDate));
            return new ServiceOk<ScalarReadModel<bool>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<bool>>(GetYieldCurveRateExistsQuery.ErrorId, ex.Message);
        }
    }

    public Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
    {
        try
        {
            var result = new ScalarReadModel<DateOnly>(CalculateValueDate(DateTime.Now));
            return Task.FromResult<ServiceResult<ScalarReadModel<DateOnly>>>(
                new ServiceOk<ScalarReadModel<DateOnly>>(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ServiceResult<ScalarReadModel<DateOnly>>>(
                new ServiceFailed<ScalarReadModel<DateOnly>>(GetValueDateQuery.ErrorId, ex.Message));
        }
    }

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
            var securitiesDb = _dbFactory.SecuritiesDb;
            var underlying = await securitiesDb.GetFuturesContractAsync(underlyingContractId);
            var shortPut = await securitiesDb.GetFuturesOptionContractAsync(shortPutOptionContractId);
            var longPut = await securitiesDb.GetFuturesOptionContractAsync(longPutOptionContractId);
            var shortCall = await securitiesDb.GetFuturesOptionContractAsync(shortCallOptionContractId);
            var longCall = await securitiesDb.GetFuturesOptionContractAsync(longCallOptionContractId);
            var yieldCurve = await _dbFactory.MarketDataDb.GetLastYieldCurveRateAsync();
            var tradingDates = await _dbFactory.MarketDataDb.GetTradingDatesAsync(
                startDate, endDate, marketType, currencyType);

            var result = new IronCondorMarketDataReadModel(
                underlying!, shortPut!, longPut!, shortCall!, longCall!,
                (yieldCurve?.OneMonth ?? 0) / 100,
                tradingDates.Length);
            return new ServiceOk<IronCondorMarketDataReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<IronCondorMarketDataReadModel>(
                GetIronCondorMarketDataQuery.ErrorId,
                ex.Message);
        }
    }

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
}
