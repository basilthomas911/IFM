using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;

public static class GetEconomicCalendar
{
    /// <summary>
    /// Gets the economic calendar based on the provided query parameters and sends a reply with the result.
    /// </summary>
    /// <param name="q">The economic calendar query.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask<EconomicCalendarReadModel[]> ExecuteAsync(
        this GetEconomicCalendarQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(q);
        return [.. await (GetEconomicCalendarAsync(
            dbFactory.MarketDataDb, q.TodaysDate, q.CalendarViewType, q.CountryCode, cancellationToken)).ConfigureAwait(false)];
    }

    /// <summary>
    /// Gets the economic calendar data from the database based on the specified parameters.
    /// </summary>
    /// <param name="db">The reference database context.</param>
    /// <param name="todaysDate">The date for which to retrieve calendar data.</param>
    /// <param name="calendarViewType">The type of calendar view.</param>
    /// <param name="countryCode">The country code for which to retrieve calendar data.</param>
    /// <returns>A collection of economic calendar read models.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the calendar view type is invalid.</exception>
    /// <returns></returns>
    internal static async ValueTask<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAsync(
        IMarketDataDbContext db,
        DateTime todaysDate,
        EconomicCalendarViewType calendarViewType,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        if (calendarViewType is EconomicCalendarViewType.ThisWeek or EconomicCalendarViewType.NextWeek)
        {
            var startDay = calendarViewType == EconomicCalendarViewType.ThisWeek
                ? GetThisWeekStartingDate(todaysDate)
                : GetNextWeekStartingDate(todaysDate);
            var startDate = startDay.Add(todaysDate.TimeOfDay);
            var endDate = startDate.AddDays(7).AddTicks(-1);
            return await (db.GetEconomicCalendarsAsync(startDate, endDate, countryCode, cancellationToken)).ConfigureAwait(false);
        }

        return calendarViewType switch
        {
            EconomicCalendarViewType.Today => await (ReadDateAsync(todaysDate)).ConfigureAwait(false),
            EconomicCalendarViewType.Tomorrow => await (ReadDateAsync(todaysDate.AddDays(1))).ConfigureAwait(false),
            EconomicCalendarViewType.Yesterday => await (ReadDateAsync(todaysDate.AddDays(-1))).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(calendarViewType), calendarViewType, "Invalid calendar view type")
        };

        Task<ICollection<EconomicCalendarReadModel>> ReadDateAsync(DateTime startDate)
        {
            var endDate = startDate.AddDays(1).AddTicks(-1);
            return db.GetEconomicCalendarsAsync(startDate, endDate, countryCode, cancellationToken);
        }
    }

    internal static DateTime GetThisWeekStartingDate(this DateTime todaysDate)
    {
        var daysSinceMonday = ((int)todaysDate.DayOfWeek + 6) % 7;
        return todaysDate.Date.AddDays(-daysSinceMonday);
    }

    internal static DateTime GetNextWeekStartingDate(this DateTime todaysDate)
    {
        return GetThisWeekStartingDate(todaysDate).AddDays(7);
    }
}
