using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

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
    public static async ValueTask GetEconomicCalendarAsync(this GetEconomicCalendarQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await GetEconomicCalendarAsync(dbFactory.ReferenceDb, q.TodaysDate, q.CalendarViewType, q.CountryCode);
        await context.ReplyAsync(q.Subject.ThreadId, GetEconomicCalendarQuery.Verb, new ServiceResult<EconomicCalendarReadModel[]>([.. result]));
    }

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
    internal static async ValueTask<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAsync(IReferenceDbContext db, DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
        => calendarViewType switch
        {
            EconomicCalendarViewType.Today => await db.GetEconomicCalendarsAsync(todaysDate, countryCode),
            EconomicCalendarViewType.Tomorrow => await db.GetEconomicCalendarsAsync(todaysDate.AddDays(1).Date, countryCode),
            EconomicCalendarViewType.Yesterday => await db.GetEconomicCalendarsAsync(todaysDate.AddDays(-1).Date, countryCode),
            EconomicCalendarViewType.ThisWeek => await db.GetEconomicCalendarsAsync(
                startDate: GetThisWeekStartingDate(todaysDate),
                endDate: GetThisWeekStartingDate(todaysDate).AddDays(7).AddMilliseconds(-1),
                countryCode: countryCode),
            EconomicCalendarViewType.NextWeek => await db.GetEconomicCalendarsAsync(
                startDate: GetNextWeekStartingDate(todaysDate),
                endDate: GetNextWeekStartingDate(todaysDate).AddDays(7).AddMilliseconds(-1),
                countryCode: countryCode),
            _ => throw new NotImplementedException($"Invalid CalendarViewType: {calendarViewType}")
        };

    internal static DateTime GetThisWeekStartingDate(this DateTime todaysDate)
    {
        var thisWeekStartingDate = todaysDate;
        while (thisWeekStartingDate.DayOfWeek != DayOfWeek.Monday)
            thisWeekStartingDate = thisWeekStartingDate.AddDays(-1);
        return thisWeekStartingDate.Date;
    }

    internal static DateTime GetNextWeekStartingDate(this DateTime todaysDate)
    {
        var nextWeekStartingDate = todaysDate;
        while (nextWeekStartingDate.DayOfWeek != DayOfWeek.Monday)
            nextWeekStartingDate = nextWeekStartingDate.AddDays(1);
        return nextWeekStartingDate.Date;
    }
}
