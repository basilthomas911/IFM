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

    public Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync()
        => GetLookupTypesCoreAsync("MarketDataDefinitionType");

    public Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync()
        => GetLookupTypesCoreAsync("ReferenceDataDefinitionType");

    public Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync()
        => GetLookupTypesCoreAsync("SystemAdminFunctionType");

    public Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync()
        => ExecuteAsync(GetLookupTypesQuery.ErrorId,
            async () => new LookupTypeCollection([.. await _dbFactory.ReferenceDb.GetLookupTypesAsync()]));

    public Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName)
        => GetLookupTypesCoreAsync(lookupTypeName);

    public Task<ServiceResult<string[]>> GetLookupTypeNamesAsync()
        => ExecuteAsync<string[]>(GetLookupTypeNamesQuery.ErrorId,
            async () => [.. await _dbFactory.ReferenceDb.GetLookupTypeNamesAsync()]);

    public Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName)
        => ExecuteAsync<LookupTypeShortCodeReadModel[]>(GetLookupTypeShortCodesQuery.ErrorId,
            async () => [.. await _dbFactory.ReferenceDb.GetLookupTypeShortCodesAsync(lookupTypeName)]);

    public Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
        => ExecuteAsync(GetNextSeedIdQuery.ErrorId,
            async () => new ScalarReadModel<int>(await _dbFactory.ReferenceDb.GetNextSeedIdAsync(seedType)));

    public Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
        => ExecuteAsync(GetCurrentSeedIdQuery.ErrorId,
            async () => new ScalarReadModel<int>(await _dbFactory.ReferenceDb.GetCurrentSeedIdAsync(seedType)));

    public Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync()
        => ExecuteAsync(GetDefaultFuturesContractDefinitionsQuery.ErrorId, async () =>
        {
            var db = _dbFactory.ReferenceDb;
            return new DefaultFuturesContractDefinitionsReadModel
            {
                Currency = (await db.GetLookupTypeAsync("DefaultFuturesContractCurrency")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Exchange = (await db.GetLookupTypeAsync("DefaultFuturesContractExchange")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Multiplier = (await db.GetLookupTypeAsync("DefaultFuturesContractMultiplier")).FirstOrDefault()?.ShortCode ?? string.Empty,
                SecurityType = (await db.GetLookupTypeAsync("DefaultFuturesContractSecurityType")).FirstOrDefault()?.ShortCode ?? string.Empty,
                OptionSecurityType = (await db.GetLookupTypeAsync("DefaultFuturesOptionContractSecurityType")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Symbol = (await db.GetLookupTypeAsync("DefaultFuturesContractSymbol")).FirstOrDefault()?.ShortCode ?? string.Empty
            };
        });

    public Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync()
        => ExecuteAsync(GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId, async () =>
        {
            var db = _dbFactory.ReferenceDb;
            return new FuturesOptionStrikePriceReadModel
            {
                Minimum = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceMin")).FirstOrDefault()?.ShortCode),
                Maximum = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceMax")).FirstOrDefault()?.ShortCode),
                Increment = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceIncrement")).FirstOrDefault()?.ShortCode)
            };
        });

    public Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(
        string lookupTypeName, string shortCode)
        => ExecuteAsync(GetLookupTypeShortCodeExistsQuery.ErrorId, async () =>
        {
            var values = await _dbFactory.ReferenceDb.GetLookupTypeShortCodesAsync(lookupTypeName);
            return new ScalarReadModel<bool>(values.Any(
                value => value.ShortCode.Equals(shortCode, StringComparison.OrdinalIgnoreCase)));
        });

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode)
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarQuery.ErrorId,
            async () => [.. await GetEconomicCalendar.GetEconomicCalendarAsync(
                _dbFactory.ReferenceDb, todaysDate, calendarType, countryCode)]);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarAllQuery.ErrorId,
            async () => [.. await _dbFactory.ReferenceDb.GetEconomicCalendarAllAsync()]);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetExternalEconomicCalendarsQuery.ErrorId, async () =>
        {
            if (_dbFactory.EconomicCalendarsDb is not IEconomicCalendarsDbContext db)
                return [];
            return [.. await db.ReadAsync()];
        });

    public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType)
        => ExecuteAsync(GetEconomicCalendarDateQuery.ErrorId,
            () => Task.FromResult(GetEconomicCalendarDate.GetEconomicCalendarEventDate(todaysDate, calendarType)));

    public Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
        => ExecuteAsync<EconomicCalendarCountryCodeReadModel[]>(GetEconomicCalendarCountryCodesQuery.ErrorId,
            async () => [.. await _dbFactory.ReferenceDb.GetEconomicCalendarCountryCodesAsync()]);

    public Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        => ExecuteAsync<MDIForwardLossRatioReadModel[]>(GetMDIForwardLossRatiosQuery.ErrorId,
            async () => [.. await _dbFactory.ReferenceDb.GetMDIForwardLossRatiosAsync(trendDirection, tradeType)]);

    Task<ServiceResult<LookupTypeCollection>> GetLookupTypesCoreAsync(string lookupTypeName)
        => ExecuteAsync(GetLookupTypeQuery.ErrorId,
            async () => new LookupTypeCollection([.. await _dbFactory.ReferenceDb.GetLookupTypeAsync(lookupTypeName)]));

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
