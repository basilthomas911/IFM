using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Queries;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

public static class GetEconomicCalendarDate
{
    /// <summary>
    /// Gets the economic calendar date based on the provided query parameters and replies with the result.
    /// </summary>
    /// <param name="q">The economic calendar date query.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask GetEconomicCalendarDateAsync(this GetEconomicCalendarDateQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var calendarDate = new ServiceResult<string>(GetEconomicCalendarEventDate(q.TodaysDate, q.CalendarViewType));
        await context.ReplyAsync(q.Subject.ThreadId, GetEconomicCalendarAllQuery.Verb, calendarDate);
    }

    /// <summary>
    /// Gets the economic calendar event date based on the provided date and calendar view type.
    /// </summary>
    /// <param name="todaysDate">The date for which to retrieve calendar data.</param>
    /// <param name="calendarViewType">The type of calendar view.</param>
    /// <returns>The economic calendar event date.</returns>
    /// <exception cref="NotImplementedException">Thrown when the calendar view type is not implemented.</exception>
    internal static string GetEconomicCalendarEventDate(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
    {
        var calendarDate = calendarViewType switch
        {
            EconomicCalendarViewType.Today => todaysDate,
            EconomicCalendarViewType.Tomorrow => todaysDate.AddDays(1).Date,
            EconomicCalendarViewType.Yesterday => todaysDate.AddDays(-1).Date,
            EconomicCalendarViewType.ThisWeek => todaysDate.GetThisWeekStartingDate(),
            EconomicCalendarViewType.NextWeek => todaysDate.GetNextWeekStartingDate(),
            _ => throw new NotImplementedException($"Invalid CalendarViewType: {calendarViewType}")
        };
        return $"{calendarDate.DayOfWeek}, {calendarDate:MMMM} {calendarDate:dd}, {calendarDate:yyyy}";
    }

}
