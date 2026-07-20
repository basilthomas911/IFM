using System;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.QueryParameters;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Application;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// API client for reference queries over the REST QueryService API.
/// Uses <see cref="IQueryServiceApi"/> to perform requests.
/// </summary>
public class ReferenceQueryApi(IQueryServiceApi querySvc) : IReferenceQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Returns default futures contract definitions.
    /// </summary>
    public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync()
    {
        var qryParam = new GetDefaultFuturesContractDefinitionsParameter();
        return await _querySvc.ExecuteQueryAsync<DefaultFuturesContractDefinitionsReadModel>(ReferenceQueryUriPath.GetDefaultFuturesContractDefinitions, qryParam, GetDefaultFuturesContractDefinitionsQuery.ErrorId);
    }

    /// <summary>
    /// Returns lookup types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync()
    {
        var qryParam = new GetLookupTypesParameter();
        return await _querySvc.ExecuteQueryAsync<LookupTypeCollection>(ReferenceQueryUriPath.GetLookupTypes, qryParam, GetLookupTypesQuery.ErrorId);
    }

    /// <summary>
    /// Returns lookup types for the specified lookup type name.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName)
    {
        var qryParam = new GetLookupTypeParameter(lookupTypeName);
        return await _querySvc.ExecuteQueryAsync<LookupTypeCollection>(ReferenceQueryUriPath.GetLookupType, qryParam, GetLookupTypeQuery.ErrorId);
    }

    /// <summary>
    /// Returns lookup type names.
    /// </summary>
    public async Task<ServiceResult<string[]>> GetLookupTypeNamesAsync()
    {
        var qryParam = new GetLookupTypeNamesParameter();
        return await _querySvc.ExecuteQueryAsync<string[]>(ReferenceQueryUriPath.GetLookupTypeNames, qryParam, GetLookupTypeNamesQuery.ErrorId);
    }

    /// <summary>
    /// Returns lookup type short codes for a lookup type name.
    /// </summary>
    public async Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName)
    {
        var qryParam = new GetLookupTypeShortCodesParameter(lookupTypeName);
        return await _querySvc.ExecuteQueryAsync<LookupTypeShortCodeReadModel[]>(ReferenceQueryUriPath.GetLookupTypeShortCodes, qryParam, GetLookupTypeShortCodesQuery.ErrorId);
    }

    /// <summary>
    /// Returns market data definition types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync()
    {
        var qryParam = new GetLookupTypeParameter("MarketDataDefinitionType");
        return await _querySvc.ExecuteQueryAsync<LookupTypeCollection>(ReferenceQueryUriPath.GetMarketDataDefinitionTypes, qryParam, GetLookupTypeQuery.ErrorId);
    }

    /// <summary>
    /// Returns reference data definition types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync()
    {
        var qryParam = new GetLookupTypeParameter("ReferenceDataDefinitionType");
        return await _querySvc.ExecuteQueryAsync<LookupTypeCollection>(ReferenceQueryUriPath.GetReferenceDataDefinitionTypes, qryParam, GetLookupTypeQuery.ErrorId);
    }

    /// <summary>
    /// Returns system admin function types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync()
    {
        var qryParam = new GetLookupTypeParameter("SystemAdminFunctionType");
        return await _querySvc.ExecuteQueryAsync<LookupTypeCollection>(ReferenceQueryUriPath.GetSystemAdminFunctionTypes, qryParam, GetLookupTypeQuery.ErrorId);
    }

    /// <summary>
    /// Returns next seed id for a seed type.
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
    {
        var qryParam = new GetNextSeedIdParameter(seedType);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(ReferenceQueryUriPath.GetNextSeedId, qryParam, GetNextSeedIdQuery.ErrorId);
    }

    /// <summary>
    /// Returns current seed id for a seed type.
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
    {
        var qryParam = new GetCurrentSeedIdParameter(seedType);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(ReferenceQueryUriPath.GetCurrentSeedId, qryParam, GetCurrentSeedIdQuery.ErrorId);
    }

    /// <summary>
    /// Returns futures option strike price definitions.
    /// </summary>
    public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync()
    {
        var qryParam = new GetFuturesOptionStrikePriceDefinitionsParameter();
        return await _querySvc.ExecuteQueryAsync<FuturesOptionStrikePriceReadModel>(ReferenceQueryUriPath.GetFuturesOptionStrikePriceDefinitions, qryParam, GetFuturesOptionStrikePriceDefinitionsQuery.ErrorId);
    }

    /// <summary>
    /// Returns whether a lookup type short code exists.
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode)
    {
        var qryParam = new GetLookupTypeShortCodeExistsParameter(lookupTypeName, shortCode);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<bool>>(ReferenceQueryUriPath.LookupTypeShortCodeExists, qryParam, GetLookupTypeShortCodeExistsQuery.ErrorId);
    }

    /// <summary>
    /// Returns economic calendars for a date, view type and country.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarAsync(DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode)
    {
        var qryParam = new GetEconomicCalendarParameter(todaysDate, calendarType, countryCode);
        return await _querySvc.ExecuteQueryAsync<EconomicCalendarReadModel[]>(ReferenceQueryUriPath.GetEconomicCalendars, qryParam, GetEconomicCalendarQuery.ErrorId);
    }

    /// <summary>
    /// Returns all economic calendars.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
    {
        var qryParam = new GetEconomicCalendarAllParameter();
        return await _querySvc.ExecuteQueryAsync<EconomicCalendarReadModel[]>(ReferenceQueryUriPath.GetEconomicCalendarAll, qryParam, GetEconomicCalendarAllQuery.ErrorId);
    }

    /// <summary>
    /// Returns external economic calendars.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
    {
        var qryParam = new GetExternalEconomicCalendarsParameter();
        return await _querySvc.ExecuteQueryAsync<EconomicCalendarReadModel[]>(ReferenceQueryUriPath.GetExternalEconomicCalendars, qryParam, GetExternalEconomicCalendarsQuery.ErrorId);
    }

    /// <summary>
    /// Returns economic calendar date string for the given parameters.
    /// </summary>
    public async Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
    {
        var qryParam = new GetEconomicCalendarDateParameter(todaysDate, calendarViewType);
        return await _querySvc.ExecuteQueryAsync<string>(ReferenceQueryUriPath.GetEconomicCalendarDate, qryParam, GetEconomicCalendarDateQuery.ErrorId);
    }

    /// <summary>
    /// Returns economic calendar country codes.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
    {
        var qryParam = new GetEconomicCalendarCountryCodesParameter();
        return await _querySvc.ExecuteQueryAsync<EconomicCalendarCountryCodeReadModel[]>(ReferenceQueryUriPath.GetEconomicCalendarCountryCodes, qryParam, GetEconomicCalendarCountryCodesQuery.ErrorId);
    }

    /// <summary>
    /// Returns MDI forward loss ratios for a trend direction and trade type.
    /// </summary>
    public async Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
    {
        var qryParam = new GetMDIForwardLossRatiosParameter(trendDirection, tradeType);
        return await _querySvc.ExecuteQueryAsync<MDIForwardLossRatioReadModel[]>(ReferenceQueryUriPath.GetMDIForwardLossRatios, qryParam, GetMDIForwardLossRatiosQuery.ErrorId);
    }

    /// <summary>
    /// Alias to support interface variations: returns economic calendars for a date, view type and country.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
        => await GetEconomicCalendarAsync(todaysDate, calendarViewType, countryCode);
}

