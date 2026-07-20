using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReferenceController(IQueryService queryService, ILogger<ReferenceController> logger) : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "ReferenceController";

    [HttpGet]
    [Route("NextSeedId")]
    [SwaggerOperation(Summary = "return next seed id")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<int>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
        => await ExecuteQueryAsync(new GetNextSeedIdQuery(seedType));

    [HttpGet]
    [Route("CurrentSeedId")]
    [SwaggerOperation(Summary = "return current seed id")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<int>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
        => await ExecuteQueryAsync(new GetCurrentSeedIdQuery (seedType));

    [HttpGet]
    [Route("LookupType")]
    [SwaggerOperation(Summary = "return lookup type")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<LookupTypeReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypeAsync(string lookupTypeName)
        => await ExecuteQueryAsync(new GetLookupTypeQuery(lookupTypeName));

    [HttpGet]
    [Route("LookupTypes")]
    [SwaggerOperation(Summary = "return lookup types")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<LookupTypeReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync()
        => await ExecuteQueryAsync(new GetLookupTypesQuery { });

    [HttpGet]
    [Route("DefaultFuturesContractDefinitions")]
    [SwaggerOperation(Summary = "return default futures contract definitions")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<DefaultFuturesContractDefinitionsReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync()
        => await ExecuteQueryAsync(new GetDefaultFuturesContractDefinitionsQuery { });

    [HttpGet]
    [Route("LookupTypeShortCodeExist")]
    [SwaggerOperation(Summary = "return lookup type code exist")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<bool>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarReadModel<bool>>> GetLookupTypeShortCodeExistAsync(string lookupTypeName, string shortCode)
        => await ExecuteQueryAsync(new GetLookupTypeShortCodeExistsQuery(lookupTypeName, shortCode));

    [HttpGet]
    [Route("FuturesOptionStrikePriceDefinitions")]
    [SwaggerOperation(Summary = "return lookup type code exist")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesOptionStrikePriceReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync()
        => await ExecuteQueryAsync(new GetFuturesOptionStrikePriceDefinitionsQuery ());

    [HttpGet]
    [Route("EconomicCalendar")]
    [SwaggerOperation(Summary = "return economic calendars by country code")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<EconomicCalendarReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> ExecuteAsync(DateOnly todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
         => await ExecuteQueryAsync(new GetEconomicCalendarQuery(todaysDate, calendarViewType, countryCode));

    [HttpGet]
    [Route("EconomicCalendarCountryCodes")]
    [SwaggerOperation(Summary = "return economic calendars country codes")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<EconomicCalendarCountryCodeReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
        => await ExecuteQueryAsync(new GetEconomicCalendarCountryCodesQuery());

    [HttpGet]
    [Route("ExternalEconomicCalendars")]
    [SwaggerOperation(Summary = "return external economic calendars")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<EconomicCalendarReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
        => await ExecuteQueryAsync(new GetExternalEconomicCalendarsQuery());

    [HttpGet]
    [Route("EconomicCalendarAll")]
    [SwaggerOperation(Summary = "return all economic calendars")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<EconomicCalendarReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarAllAsync()
        => await ExecuteQueryAsync(new GetEconomicCalendarAllQuery());

    [HttpGet]
    [Route("EconomicCalendarDate")]
    [SwaggerOperation(Summary = "return economic calendar date")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<string>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
        => await ExecuteQueryAsync(new GetEconomicCalendarDateQuery(todaysDate, calendarViewType));

    [HttpGet]
    [Route("LookupTypeNames")]
    [SwaggerOperation(Summary = "return lookup type names")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<string[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<string[]>> GetLookupTypeNamesAsync()
        => await ExecuteQueryAsync(new GetLookupTypeNamesQuery());

    [HttpGet]
    [Route("LookupTypeShortCodes")]
    [SwaggerOperation(Summary = "return lookup type short codes")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<LookupTypeShortCodeReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName)
        => await ExecuteQueryAsync(new GetLookupTypeShortCodesQuery(lookupTypeName));

    [HttpGet]
    [Route("MDIForwardLossRatios")]
    [SwaggerOperation(Summary = "return MDI forward loss ratios")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<MDIForwardLossRatioReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        => await ExecuteQueryAsync(new GetMDIForwardLossRatiosQuery(trendDirection, tradeType));

}