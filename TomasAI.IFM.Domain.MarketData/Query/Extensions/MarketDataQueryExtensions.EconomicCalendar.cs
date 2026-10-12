#pragma warning disable CA1034 // C# 14 extension blocks generate nested metadata types; no public nested type is declared here.
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query.Extensions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
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
            => ExecuteAsync<EconomicCalendarPageReadModel>(GetEconomicCalendarPageQuery.ErrorId, async () => await (context.DbFactory.MarketDataDb.GetEconomicCalendarPageAsync(request, cancellationToken)).ConfigureAwait(false), cancellationToken);

        public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
            DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode)
            => context.GetEconomicCalendarsAsync(todaysDate, calendarType, countryCode, CancellationToken.None);

        public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(
            DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode, CancellationToken cancellationToken)
            => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarQuery.ErrorId, async () => [.. await (GetEconomicCalendar.GetEconomicCalendarAsync(
                context.DbFactory.MarketDataDb, todaysDate, calendarType, countryCode, cancellationToken)).ConfigureAwait(false)], cancellationToken);

        public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
            => context.GetEconomicCalendarsAsync(CancellationToken.None);

        public Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(CancellationToken cancellationToken)
            => ExecuteAsync<EconomicCalendarReadModel[]>(GetEconomicCalendarAllQuery.ErrorId, async () => [.. await (context.DbFactory.MarketDataDb.GetEconomicCalendarAllAsync(cancellationToken)).ConfigureAwait(false)], cancellationToken);

        public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarType)
            => context.GetEconomicCalendarDateAsync(todaysDate, calendarType, CancellationToken.None);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "This C# 14 extension member preserves the typed query context API even though this date-only operation needs no context data.")]
        public Task<ServiceResult<string>> GetEconomicCalendarDateAsync(
            DateTime todaysDate, EconomicCalendarViewType calendarType, CancellationToken cancellationToken)
            => ExecuteAsync(GetEconomicCalendarDateQuery.ErrorId, () => Task.FromResult(GetEconomicCalendarDate.GetEconomicCalendarEventDate(todaysDate, calendarType)), cancellationToken);

        public Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
            => context.GetEconomicCalendarCountryCodesAsync(CancellationToken.None);

        public Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken)
            => ExecuteAsync<EconomicCalendarCountryCodeReadModel[]>(GetEconomicCalendarCountryCodesQuery.ErrorId, async () => [.. await (context.DbFactory.MarketDataDb.GetEconomicCalendarCountryCodesAsync(cancellationToken)).ConfigureAwait(false)], cancellationToken);
    }
}

#pragma warning restore CA1034
