namespace TomasAI.IFM.Domain.Trade.Shared;

public static class DailyFuturesSession
{
    static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    public static DateTime FlattenUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(15,55)), Eastern);
    public static bool EntryAllowed(DateTime at)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(at, Eastern);
        return local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && local.TimeOfDay >= new TimeSpan(9,30,0) && local.TimeOfDay < new TimeSpan(15,55,0);
    }
}
