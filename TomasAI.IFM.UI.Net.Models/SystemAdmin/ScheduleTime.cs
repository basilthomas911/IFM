namespace TomasAI.IFM.UI.Net.Models.SystemAdmin;

/// <summary>Converts schedule editor/display values using the schedule's explicit zone, never the workstation zone.</summary>
public static class ScheduleTime
{
    public static DateTimeOffset FromUtc(DateTimeOffset instant, string timeZoneId)
        => TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

    public static DateTimeOffset ToUtc(DateTime wallClock, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new ArgumentException("The schedule bound is invalid or ambiguous in its selected time zone. Choose an unambiguous time.", nameof(wallClock));
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }
}
