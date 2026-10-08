namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

/// <summary>Resolves the same stored IANA or Windows zone for preview and Quartz application.</summary>
public static class SchedulerTimeZone
{
    /// <summary>Resolves a supported zone without falling back to the machine's local zone.</summary>
    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)) return zone;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var iana)
            && TimeZoneInfo.TryFindSystemTimeZoneById(iana, out zone)) return zone;
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windows)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windows, out zone)) return zone;
        throw new TimeZoneNotFoundException($"Scheduled-task time zone '{timeZoneId}' is unavailable.");
    }
}
