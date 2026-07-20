using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetValueDate
{
    /// Handles a <see cref="GetValueDateQuery"/> by calculating the current value date based on the current date and time.
    /// The value date is determined according to the following rules:
    /// - If today is Sunday and the time is 18:00 or later, the value date is Monday (tomorrow).
    /// - If today is Monday-Thursday and the time is 18:00 or later, the value date is the next day. If the time is 17:00 or earlier, the value date is today.
    /// - If today is Friday and the time is 17:00 or earlier, the value date is today. If the time is after 17:00, the value date remains today (no change).
    /// The calculated value date is then published back to the caller via a NATS reply.    
    /// <param name="q">The query requesting the current value date.</param>
    /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    public static async ValueTask GetValueDateAsync(this GetValueDateQuery q, IQueryActorContext context)
    {
        var today = DateTime.Now;
        var valueDate = default(ScalarReadModel<DateOnly>);
        switch (today.DayOfWeek)
        {
            case DayOfWeek.Sunday:
                if (today.TimeOfDay >= TimeSpan.FromHours(18))
                    valueDate = new ScalarReadModel<DateOnly>(new DateOnly(today.Year, today.Month, today.Day).AddDays(1));
                break;
            case DayOfWeek.Monday:
            case DayOfWeek.Tuesday:
            case DayOfWeek.Wednesday:
            case DayOfWeek.Thursday:
                if (today.TimeOfDay >= TimeSpan.FromHours(18))
                    valueDate = new ScalarReadModel<DateOnly>(new DateOnly(today.Year, today.Month, today.Day).AddDays(1));
                else if (today.TimeOfDay <= TimeSpan.FromHours(17))
                    valueDate = new ScalarReadModel<DateOnly>(new DateOnly(today.Year, today.Month, today.Day));
                break;
            case DayOfWeek.Friday:
                if (today.TimeOfDay <= TimeSpan.FromHours(17))
                    valueDate = new ScalarReadModel<DateOnly>(new DateOnly(today.Year, today.Month, today.Day));
                break;
        }
        await context.ReplyAsync(q.Subject.ThreadId, GetValueDateQuery.Verb, new ServiceResult<ScalarReadModel<DateOnly>>(valueDate!));
    }
}
