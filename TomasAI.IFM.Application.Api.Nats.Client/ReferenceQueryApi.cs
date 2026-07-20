using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.QueryParameters;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class ReferenceQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IReferenceQueryApi
{
    /// <summary>
    /// Returns default futures contract definitions.
    /// </summary>
    public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync()
    {
        var entityId = new GetDefaultFuturesContractDefinitionsParameter();
        GetDefaultFuturesContractDefinitionsQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetDefaultFuturesContractDefinitionsQuery.Actor, GetDefaultFuturesContractDefinitionsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetDefaultFuturesContractDefinitionsQuery, DefaultFuturesContractDefinitionsReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Returns lookup types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync()
    {
        var entityId = new GetLookupTypesParameter();
        GetLookupTypesQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypesQuery.Actor, GetLookupTypesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypesQuery, LookupTypeCollection>(query.Subject, query);
    }

    /// <summary>
    /// Returns lookup types for the specified lookup type name.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName)
    {
        var entityId = new GetLookupTypeParameter(lookupTypeName);
        GetLookupTypeQuery query = new(lookupTypeName)
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypeQuery, LookupTypeCollection>(query.Subject, query);
    }

    /// <summary>
    /// Returns lookup type names.
    /// </summary>
    public async Task<ServiceResult<string[]>> GetLookupTypeNamesAsync()
    {
        var entityId = new GetLookupTypeNamesParameter();
        GetLookupTypeNamesQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypeNamesQuery.Actor, GetLookupTypeNamesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypeNamesQuery, string[]>(query.Subject, query);
    }

    /// <summary>
    /// Returns lookup type short codes for a lookup type name.
    /// </summary>
    public async Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName)
    {
        var entityId = new GetLookupTypeShortCodesParameter(lookupTypeName);
        GetLookupTypeShortCodesQuery query = new(lookupTypeName)
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypeShortCodesQuery.Actor, GetLookupTypeShortCodesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypeShortCodesQuery, LookupTypeShortCodeReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Returns market data definition types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync()
    {
        var entityId = new GetLookupTypeParameter("MarketDataDefinitionType");
        GetLookupTypeQuery query = new("MarketDataDefinitionType")
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypeQuery, LookupTypeCollection>(query.Subject, query);
    }

    /// <summary>
    /// Returns reference data definition types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync()
    {
        var entityId = new GetLookupTypeParameter("ReferenceDataDefinitionType");
        GetLookupTypeQuery query = new("ReferenceDataDefinitionType")
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypeQuery, LookupTypeCollection>(query.Subject, query);
    }

    /// <summary>
    /// Returns system admin function types.
    /// </summary>
    public async Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync()
    {
        var entityId = new GetLookupTypeParameter("SystemAdminFunctionType");
        GetLookupTypeQuery query = new("SystemAdminFunctionType")
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypeQuery, LookupTypeCollection>(query.Subject, query);
    }

    /// <summary>
    /// Returns next seed id for a seed type.
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
    {
        var entityId = new GetNextSeedIdParameter(seedType);
        GetNextSeedIdQuery query = new(seedType)
        {
            Subject = new ActorSubject(ActorType.Query, GetNextSeedIdQuery.Actor, GetNextSeedIdQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetNextSeedIdQuery, ScalarReadModel<int>>(query.Subject, query);
    }

    /// <summary>
    /// Returns current seed id for a seed type.
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
    {
        var entityId = new GetCurrentSeedIdParameter(seedType);
        GetCurrentSeedIdQuery query = new(seedType)
        {
            Subject = new ActorSubject(ActorType.Query, GetCurrentSeedIdQuery.Actor, GetCurrentSeedIdQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetCurrentSeedIdQuery, ScalarReadModel<int>>(query.Subject, query);
    }

    /// <summary>
    /// Returns futures option strike price definitions.
    /// </summary>
    public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync()
    {
        var entityId = new GetFuturesOptionStrikePriceDefinitionsParameter();
        GetFuturesOptionStrikePriceDefinitionsQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionStrikePriceDefinitionsQuery.Actor, GetFuturesOptionStrikePriceDefinitionsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFuturesOptionStrikePriceDefinitionsQuery, FuturesOptionStrikePriceReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Returns whether a lookup type short code exists.
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode)
    {
        var entityId = new GetLookupTypeShortCodeExistsParameter(lookupTypeName, shortCode);
        GetLookupTypeShortCodeExistsQuery query = new(lookupTypeName, shortCode)
        {
            Subject = new ActorSubject(ActorType.Query, GetLookupTypeShortCodeExistsQuery.Actor, GetLookupTypeShortCodeExistsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetLookupTypeShortCodeExistsQuery, ScalarReadModel<bool>>(query.Subject, query);
    }

    /// <summary>
    /// Returns economic calendars for a date, view type and country.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarAsync(DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode)
    {
        var entityId = new GetEconomicCalendarParameter(todaysDate, calendarType, countryCode);
        GetEconomicCalendarQuery query = new(todaysDate, calendarType, countryCode)
        {
            Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarQuery.Actor, GetEconomicCalendarQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetEconomicCalendarQuery, EconomicCalendarReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Returns all economic calendars.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
    {
        var entityId = new GetEconomicCalendarAllParameter();
        GetEconomicCalendarAllQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarAllQuery.Actor, GetEconomicCalendarAllQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetEconomicCalendarAllQuery, EconomicCalendarReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Returns external economic calendars.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
    {
        var entityId = new GetExternalEconomicCalendarsParameter();
        GetExternalEconomicCalendarsQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetExternalEconomicCalendarsQuery.Actor, GetExternalEconomicCalendarsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetExternalEconomicCalendarsQuery, EconomicCalendarReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Returns economic calendar date string for the given parameters.
    /// </summary>
    public async Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
    {
        var entityId = new GetEconomicCalendarDateParameter(todaysDate, calendarViewType);
        GetEconomicCalendarDateQuery query = new(todaysDate, calendarViewType)
        {
            Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarDateQuery.Actor, GetEconomicCalendarDateQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetEconomicCalendarDateQuery, string>(query.Subject, query);
    }

    /// <summary>
    /// Returns economic calendar country codes.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
    {
        var entityId = new GetEconomicCalendarCountryCodesParameter();
        GetEconomicCalendarCountryCodesQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarCountryCodesQuery.Actor, GetEconomicCalendarCountryCodesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetEconomicCalendarCountryCodesQuery, EconomicCalendarCountryCodeReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Returns MDI forward loss ratios for a trend direction and trade type.
    /// </summary>
    public async Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
    {
        var entityId = new GetMDIForwardLossRatiosParameter(trendDirection, tradeType);
        GetMDIForwardLossRatiosQuery query = new(trendDirection, tradeType)
        {
            Subject = new ActorSubject(ActorType.Query, GetMDIForwardLossRatiosQuery.Actor, GetMDIForwardLossRatiosQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetMDIForwardLossRatiosQuery, MDIForwardLossRatioReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Alias to support interface variations: returns economic calendars for a date, view type and country.
    /// </summary>
    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
        => await GetEconomicCalendarAsync(todaysDate, calendarViewType, countryCode);

  
}

