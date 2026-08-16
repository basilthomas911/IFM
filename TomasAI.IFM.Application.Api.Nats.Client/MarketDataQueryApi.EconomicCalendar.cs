using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public partial class MarketDataQueryApi
{
    public async Task<ServiceResult<EconomicCalendarPageReadModel>> GetEconomicCalendarPageAsync(
        EconomicCalendarPageRequest request)
    {
        var id = new GetEconomicCalendarPageParameter(request);
        var query = new GetEconomicCalendarPageQuery(request)
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetEconomicCalendarPageQuery.Actor,
                GetEconomicCalendarPageQuery.Verb,
                id.Format())
        };
        return await RequestAsync<GetEconomicCalendarPageQuery, EconomicCalendarPageReadModel>(query.Subject, query);
    }

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(DateTime date, EconomicCalendarViewType viewType, string countryCode)
    {
        var id = new GetEconomicCalendarParameter(date, viewType, countryCode);
        var query = new GetEconomicCalendarQuery(date, viewType, countryCode)
        {
            Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarQuery.Actor, GetEconomicCalendarQuery.Verb, id.Format())
        };
        return await RequestAsync<GetEconomicCalendarQuery, EconomicCalendarReadModel[]>(query.Subject, query);
    }

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
    {
        var id = new GetEconomicCalendarAllParameter();
        var query = new GetEconomicCalendarAllQuery { Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarAllQuery.Actor, GetEconomicCalendarAllQuery.Verb, id.Format()) };
        return await RequestAsync<GetEconomicCalendarAllQuery, EconomicCalendarReadModel[]>(query.Subject, query);
    }

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
    {
        var id = new GetExternalEconomicCalendarsParameter();
        var query = new GetExternalEconomicCalendarsQuery { Subject = new ActorSubject(ActorType.Query, GetExternalEconomicCalendarsQuery.Actor, GetExternalEconomicCalendarsQuery.Verb, id.Format()) };
        return await RequestAsync<GetExternalEconomicCalendarsQuery, EconomicCalendarReadModel[]>(query.Subject, query);
    }

    public async Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime date, EconomicCalendarViewType viewType)
    {
        var id = new GetEconomicCalendarDateParameter(date, viewType);
        var query = new GetEconomicCalendarDateQuery(date, viewType) { Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarDateQuery.Actor, GetEconomicCalendarDateQuery.Verb, id.Format()) };
        return await RequestAsync<GetEconomicCalendarDateQuery, string>(query.Subject, query);
    }

    public async Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
    {
        var id = new GetEconomicCalendarCountryCodesParameter();
        var query = new GetEconomicCalendarCountryCodesQuery { Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarCountryCodesQuery.Actor, GetEconomicCalendarCountryCodesQuery.Verb, id.Format()) };
        return await RequestAsync<GetEconomicCalendarCountryCodesQuery, EconomicCalendarCountryCodeReadModel[]>(query.Subject, query);
    }
}
