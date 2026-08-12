using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query.Api;

public sealed partial class ActorMarketDataQueryApi
{
    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode)
        => GetEconomicCalendarsAsync(todaysDate, calendarType, countryCode, CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode, CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarQuery.ErrorId, cancellationToken,
            async () => [.. await GetEconomicCalendar.GetEconomicCalendarAsync(
                _dbFactory.MarketDataDb, todaysDate, calendarType, countryCode, cancellationToken)]);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
        => GetEconomicCalendarsAsync(CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarAllQuery.ErrorId, cancellationToken,
            async () => [.. await _dbFactory.MarketDataDb.GetEconomicCalendarAllAsync(cancellationToken)]);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
        => GetExternalEconomicCalendarsAsync(CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync(CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarReadModel[]>(GetExternalEconomicCalendarsQuery.ErrorId, cancellationToken,
            async () => _dbFactory.EconomicCalendarsDb is not IEconomicCalendarsDbContext db
                ? [] : [.. await db.ReadAsync(cancellationToken)]);

    public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarType)
        => GetEconomicCalendarDateAsync(todaysDate, calendarType, CancellationToken.None);

    public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(
        DateTime todaysDate, EconomicCalendarViewType calendarType, CancellationToken cancellationToken)
        => ExecuteAsync(GetEconomicCalendarDateQuery.ErrorId, cancellationToken,
            () => Task.FromResult(GetEconomicCalendarDate.GetEconomicCalendarEventDate(todaysDate, calendarType)));

    public Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
        => GetEconomicCalendarCountryCodesAsync(CancellationToken.None);

    public Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken)
        => ExecuteAsync<EconomicCalendarCountryCodeReadModel[]>(GetEconomicCalendarCountryCodesQuery.ErrorId, cancellationToken,
            async () => [.. await _dbFactory.MarketDataDb.GetEconomicCalendarCountryCodesAsync(cancellationToken)]);
}
