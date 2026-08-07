using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

public static class GetEconomicCalendar
{
    /// <summary>
    /// Gets the economic calendar based on the provided query parameters and sends a reply with the result.
    /// </summary>
    /// <param name="q">The economic calendar query.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask<EconomicCalendarReadModel[]> GetEconomicCalendarAsync(
        this GetEconomicCalendarQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        => [.. await GetEconomicCalendarAsync(
            dbFactory.ReferenceDb, q.TodaysDate, q.CalendarViewType, q.CountryCode, cancellationToken)];

    /// <summary>
    /// Gets the economic calendar data from the database based on the specified parameters.
    /// </summary>
    /// <param name="db">The reference database context.</param>
    /// <param name="todaysDate">The date for which to retrieve calendar data.</param>
    /// <param name="calendarViewType">The type of calendar view.</param>
    /// <param name="countryCode">The country code for which to retrieve calendar data.</param>
    /// <returns>A collection of economic calendar read models.</returns>
    /// <exception cref="NotImplementedException">Thrown when the calendar view type is not implemented.</exception>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    internal static async ValueTask<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAsync(
        IReferenceDbContext db,
        DateTime todaysDate,
        EconomicCalendarViewType calendarViewType,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        if (calendarViewType is EconomicCalendarViewType.ThisWeek or EconomicCalendarViewType.NextWeek)
        {
            var startDate = calendarViewType == EconomicCalendarViewType.ThisWeek
                ? GetThisWeekStartingDate(todaysDate)
                : GetNextWeekStartingDate(todaysDate);
            var endDate = startDate.AddDays(7).AddMilliseconds(-1);
            return cancellationToken.CanBeCanceled
                ? await db.GetEconomicCalendarsAsync(startDate, endDate, countryCode, cancellationToken)
                : await db.GetEconomicCalendarsAsync(startDate, endDate, countryCode);
        }

        return calendarViewType switch
        {
            EconomicCalendarViewType.Today => await ReadDateAsync(todaysDate),
            EconomicCalendarViewType.Tomorrow => await ReadDateAsync(todaysDate.AddDays(1).Date),
            EconomicCalendarViewType.Yesterday => await ReadDateAsync(todaysDate.AddDays(-1).Date),
            _ => throw new NotImplementedException($"Invalid CalendarViewType: {calendarViewType}")
        };

        Task<ICollection<EconomicCalendarReadModel>> ReadDateAsync(DateTime eventDate)
            => cancellationToken.CanBeCanceled
                ? db.GetEconomicCalendarsAsync(eventDate, countryCode, cancellationToken)
                : db.GetEconomicCalendarsAsync(eventDate, countryCode);
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
