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

/// <summary>
/// Provides direct, in-process Reference queries without actor messaging.
/// </summary>
/// <remarks>
/// Lookup, seed, futures-default, and economic-calendar data is read through Reference storage and the
/// optional external calendar reader. Every public method owns its typed success/failure mapping. The
/// implementation does not capture actor context and may be registered as a singleton.
/// </remarks>
public sealed partial class ActorReferenceQueryApi(IDbContextFactory dbFactory) : IActorReferenceQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets market data definition types.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets reference data definition types.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets system admin function types.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets lookup types.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets lookup types.
    /// </summary>
    /// <param name="lookupTypeName">The lookup-type name.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets lookup type names.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets lookup type short codes.
    /// </summary>
    /// <param name="lookupTypeName">The lookup-type name.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets next seed ID.
    /// </summary>
    /// <param name="seedType">The seed category.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets the highest seed ID currently reserved by PostgreSQL.
    /// </summary>
    /// <param name="seedType">The seed category.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets default futures contract definitions.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>>
        GetDefaultFuturesContractDefinitionsAsync()
    {
        try
        {
            var result = await GetDefaultFuturesContractDefinitions
                .GetDefaultFuturesContractDefinitionsAsync(_dbFactory.ReferenceDb);
            return new ServiceOk<DefaultFuturesContractDefinitionsReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<DefaultFuturesContractDefinitionsReadModel>(
                GetDefaultFuturesContractDefinitionsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets futures option strike price definitions.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>>
        GetFuturesOptionStrikePriceDefinitionsAsync()
    {
        try
        {
            var result = await GetFuturesOptionStrikePriceDefinitions
                .GetFuturesOptionStrikePriceDefinitionsAsync(_dbFactory.ReferenceDb);
            return new ServiceOk<FuturesOptionStrikePriceReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FuturesOptionStrikePriceReadModel>(
                GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Determines whether lookup type short code exists.
    /// </summary>
    /// <param name="lookupTypeName">The lookup-type name.</param>
    /// <param name="shortCode">The lookup short code.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(
        string lookupTypeName, string shortCode)
    {
        try
        {
            var result = new ScalarReadModel<bool>(await _dbFactory.ReferenceDb
                .LookupTypeShortCodeExistsAsync(lookupTypeName, shortCode));
            return new ServiceOk<ScalarReadModel<bool>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<bool>>(
                GetLookupTypeShortCodeExistsQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets economic calendars.
    /// </summary>
    /// <param name="todaysDate">The date used to select calendar events.</param>
    /// <param name="calendarType">The economic-calendar view type.</param>
    /// <param name="countryCode">The economic-calendar country code.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets economic calendars.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets external economic calendars.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets economic calendar date.
    /// </summary>
    /// <param name="todaysDate">The date used to select calendar events.</param>
    /// <param name="calendarType">The economic-calendar view type.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets economic calendar country codes.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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

    /// <summary>
    /// Gets MDI forward loss ratios.
    /// </summary>
    /// <param name="trendDirection">The intrinsic-time trend direction.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
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
