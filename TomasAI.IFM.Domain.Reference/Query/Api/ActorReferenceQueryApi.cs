using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.Query.Api;

/// <summary>Provides direct, in-process Reference queries without actor messaging.</summary>
public sealed class ActorReferenceQueryApi(IDbContextFactory dbFactory) : IActorReferenceQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    public async Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync()
    {
        try
        {
            LookupTypeCollection result = await GetLookupTypesCoreAsync("MarketDataDefinitionType");
            return new ServiceOk<LookupTypeCollection>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync()
    {
        try
        {
            LookupTypeCollection result = await GetLookupTypesCoreAsync("ReferenceDataDefinitionType");
            return new ServiceOk<LookupTypeCollection>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync()
    {
        try
        {
            LookupTypeCollection result = await GetLookupTypesCoreAsync("SystemAdminFunctionType");
            return new ServiceOk<LookupTypeCollection>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync()
    {
        try
        {
            var result = new LookupTypeCollection([.. await _dbFactory.ReferenceDb.GetLookupTypesAsync()]);
            return new ServiceOk<LookupTypeCollection>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<LookupTypeCollection>(GetLookupTypesQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName)
    {
        try
        {
            LookupTypeCollection result = await GetLookupTypesCoreAsync(lookupTypeName);
            return new ServiceOk<LookupTypeCollection>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<LookupTypeCollection>(GetLookupTypeQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<string[]>> GetLookupTypeNamesAsync()
    {
        try
        {
            string[] result = [.. await _dbFactory.ReferenceDb.GetLookupTypeNamesAsync()];
            return new ServiceOk<string[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string[]>(GetLookupTypeNamesQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(
        string lookupTypeName)
    {
        try
        {
            LookupTypeShortCodeReadModel[] result =
                [.. await _dbFactory.ReferenceDb.GetLookupTypeShortCodesAsync(lookupTypeName)];
            return new ServiceOk<LookupTypeShortCodeReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<LookupTypeShortCodeReadModel[]>(
                GetLookupTypeShortCodesQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
    {
        try
        {
            var result = new ScalarReadModel<int>(await _dbFactory.ReferenceDb.GetNextSeedIdAsync(seedType));
            return new ServiceOk<ScalarReadModel<int>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetNextSeedIdQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
    {
        try
        {
            var result = new ScalarReadModel<int>(await _dbFactory.ReferenceDb.GetCurrentSeedIdAsync(seedType));
            return new ServiceOk<ScalarReadModel<int>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetCurrentSeedIdQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>>
        GetDefaultFuturesContractDefinitionsAsync()
    {
        try
        {
            var db = _dbFactory.ReferenceDb;
            var result = new DefaultFuturesContractDefinitionsReadModel
            {
                Currency = (await db.GetLookupTypeAsync("DefaultFuturesContractCurrency")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Exchange = (await db.GetLookupTypeAsync("DefaultFuturesContractExchange")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Multiplier = (await db.GetLookupTypeAsync("DefaultFuturesContractMultiplier")).FirstOrDefault()?.ShortCode ?? string.Empty,
                SecurityType = (await db.GetLookupTypeAsync("DefaultFuturesContractSecurityType")).FirstOrDefault()?.ShortCode ?? string.Empty,
                OptionSecurityType = (await db.GetLookupTypeAsync("DefaultFuturesOptionContractSecurityType")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Symbol = (await db.GetLookupTypeAsync("DefaultFuturesContractSymbol")).FirstOrDefault()?.ShortCode ?? string.Empty
            };
            return new ServiceOk<DefaultFuturesContractDefinitionsReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<DefaultFuturesContractDefinitionsReadModel>(
                GetDefaultFuturesContractDefinitionsQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>>
        GetFuturesOptionStrikePriceDefinitionsAsync()
    {
        try
        {
            var db = _dbFactory.ReferenceDb;
            var result = new FuturesOptionStrikePriceReadModel
            {
                Minimum = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceMin")).FirstOrDefault()?.ShortCode),
                Maximum = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceMax")).FirstOrDefault()?.ShortCode),
                Increment = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceIncrement")).FirstOrDefault()?.ShortCode)
            };
            return new ServiceOk<FuturesOptionStrikePriceReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionStrikePriceReadModel>(
                GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(
        string lookupTypeName, string shortCode)
    {
        try
        {
            var values = await _dbFactory.ReferenceDb.GetLookupTypeShortCodesAsync(lookupTypeName);
            var result = new ScalarReadModel<bool>(values.Any(
                value => value.ShortCode.Equals(shortCode, StringComparison.OrdinalIgnoreCase)));
            return new ServiceOk<ScalarReadModel<bool>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<bool>>(
                GetLookupTypeShortCodeExistsQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode)
    {
        try
        {
            EconomicCalendarReadModel[] result =
                [.. await GetEconomicCalendar.GetEconomicCalendarAsync(
                    _dbFactory.ReferenceDb,
                    todaysDate,
                    calendarType,
                    countryCode)];
            return new ServiceOk<EconomicCalendarReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<EconomicCalendarReadModel[]>(GetEconomicCalendarQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
    {
        try
        {
            EconomicCalendarReadModel[] result =
                [.. await _dbFactory.ReferenceDb.GetEconomicCalendarAllAsync()];
            return new ServiceOk<EconomicCalendarReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<EconomicCalendarReadModel[]>(
                GetEconomicCalendarAllQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
    {
        try
        {
            if (_dbFactory.EconomicCalendarsDb is not IEconomicCalendarsDbContext db)
                return new ServiceOk<EconomicCalendarReadModel[]>([]);
            EconomicCalendarReadModel[] result = [.. await db.ReadAsync()];
            return new ServiceOk<EconomicCalendarReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<EconomicCalendarReadModel[]>(
                GetExternalEconomicCalendarsQuery.ErrorId,
                ex.Message);
        }
    }

    public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType)
    {
        try
        {
            string result = GetEconomicCalendarDate.GetEconomicCalendarEventDate(todaysDate, calendarType);
            return Task.FromResult<ServiceResult<string>>(new ServiceOk<string>(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ServiceResult<string>>(
                new ServiceFailed<string>(GetEconomicCalendarDateQuery.ErrorId, ex.Message));
        }
    }

    public async Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>>
        GetEconomicCalendarCountryCodesAsync()
    {
        try
        {
            EconomicCalendarCountryCodeReadModel[] result =
                [.. await _dbFactory.ReferenceDb.GetEconomicCalendarCountryCodesAsync()];
            return new ServiceOk<EconomicCalendarCountryCodeReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<EconomicCalendarCountryCodeReadModel[]>(
                GetEconomicCalendarCountryCodesQuery.ErrorId,
                ex.Message);
        }
    }

    public async Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection, TradeType tradeType)
    {
        try
        {
            MDIForwardLossRatioReadModel[] result =
                [.. await _dbFactory.ReferenceDb.GetMDIForwardLossRatiosAsync(trendDirection, tradeType)];
            return new ServiceOk<MDIForwardLossRatioReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<MDIForwardLossRatioReadModel[]>(
                GetMDIForwardLossRatiosQuery.ErrorId,
                ex.Message);
        }
    }

    async Task<LookupTypeCollection> GetLookupTypesCoreAsync(string lookupTypeName)
        => new([.. await _dbFactory.ReferenceDb.GetLookupTypeAsync(lookupTypeName)]);
}
