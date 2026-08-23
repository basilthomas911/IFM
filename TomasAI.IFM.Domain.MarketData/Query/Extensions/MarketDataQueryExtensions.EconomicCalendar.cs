using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query.Extensions;

public static partial class MarketDataQueryExtensions
{
    extension(IMarketDataQueryContext context)
    {
    public Task<ServiceResult<EconomicCalendarPageReadModel>> GetEconomicCalendarPageAsync(
        EconomicCalendarPageRequest request)
        => context.GetEconomicCalendarPageAsync(request, CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarPageReadModel>> GetEconomicCalendarPageAsync(
        EconomicCalendarPageRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarPageReadModel>(GetEconomicCalendarPageQuery.ErrorId, cancellationToken,
            async () => await context.DbFactory.MarketDataDb.GetEconomicCalendarPageAsync(request, cancellationToken));

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode)
        => context.GetEconomicCalendarsAsync(todaysDate, calendarType, countryCode, CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode, CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarQuery.ErrorId, cancellationToken,
            async () => [.. await GetEconomicCalendar.GetEconomicCalendarAsync(
                context.DbFactory.MarketDataDb, todaysDate, calendarType, countryCode, cancellationToken)]);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
        => context.GetEconomicCalendarsAsync(CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarAllQuery.ErrorId, cancellationToken,
            async () => [.. await context.DbFactory.MarketDataDb.GetEconomicCalendarAllAsync(cancellationToken)]);

    public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarType)
        => context.GetEconomicCalendarDateAsync(todaysDate, calendarType, CancellationToken.None);

    public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, CancellationToken cancellationToken)
        => ExecuteAsync(GetEconomicCalendarDateQuery.ErrorId, cancellationToken,
            () => Task.FromResult(GetEconomicCalendarDate.GetEconomicCalendarEventDate(todaysDate, calendarType)));

    public Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
        => context.GetEconomicCalendarCountryCodesAsync(CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarCountryCodeReadModel[]>(GetEconomicCalendarCountryCodesQuery.ErrorId, cancellationToken,
            async () => [.. await context.DbFactory.MarketDataDb.GetEconomicCalendarCountryCodesAsync(cancellationToken)]);
    }
}
