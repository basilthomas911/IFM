using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Client;

public partial class MarketDataQueryApi
{
    public async Task<ServiceResult<EconomicCalendarPageReadModel>> GetEconomicCalendarPageAsync(
        EconomicCalendarPageRequest request)
        => await _querySvc.ExecuteQueryAsync<EconomicCalendarPageReadModel>(
            MarketDataQueryUriPath.GetEconomicCalendarPage,
            new GetEconomicCalendarPageParameter(request),
            GetEconomicCalendarPageQuery.ErrorId);

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(DateTime date, EconomicCalendarViewType viewType, string countryCode)
        => await _querySvc.ExecuteQueryAsync<EconomicCalendarReadModel[]>(MarketDataQueryUriPath.GetEconomicCalendars,
            new GetEconomicCalendarParameter(date, viewType, countryCode), GetEconomicCalendarQuery.ErrorId);

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
        => await _querySvc.ExecuteQueryAsync<EconomicCalendarReadModel[]>(MarketDataQueryUriPath.GetEconomicCalendarAll,
            new GetEconomicCalendarAllParameter(), GetEconomicCalendarAllQuery.ErrorId);

    public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
        => await _querySvc.ExecuteQueryAsync<EconomicCalendarReadModel[]>(MarketDataQueryUriPath.GetExternalEconomicCalendars,
            new GetExternalEconomicCalendarsParameter(), GetExternalEconomicCalendarsQuery.ErrorId);

    public async Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime date, EconomicCalendarViewType viewType)
        => await _querySvc.ExecuteQueryAsync<string>(MarketDataQueryUriPath.GetEconomicCalendarDate,
            new GetEconomicCalendarDateParameter(date, viewType), GetEconomicCalendarDateQuery.ErrorId);

    public async Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
        => await _querySvc.ExecuteQueryAsync<EconomicCalendarCountryCodeReadModel[]>(MarketDataQueryUriPath.GetEconomicCalendarCountryCodes,
            new GetEconomicCalendarCountryCodesParameter(), GetEconomicCalendarCountryCodesQuery.ErrorId);
}
