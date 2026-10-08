using Quartz;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Validates portable Quartz timing independently of runtime installation.</summary>
public static class ScheduledTaskTimingRules
{
    /// <summary>Resolves IANA or Windows zones without a machine-local fallback.</summary>
    public static TimeZoneInfo ResolveTimeZone(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) throw new TimeZoneNotFoundException("A time zone is required.");
        if (TimeZoneInfo.TryFindSystemTimeZoneById(identity, out var zone)) return zone;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(identity, out var iana) && TimeZoneInfo.TryFindSystemTimeZoneById(iana, out zone)) return zone;
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(identity, out var windows) && TimeZoneInfo.TryFindSystemTimeZoneById(windows, out zone)) return zone;
        throw new TimeZoneNotFoundException($"Time zone '{identity}' is unavailable.");
    }
    /// <summary>Checks an intended automatic fire instant against the configured expression and zone.</summary>
    public static bool IsOccurrence(ScheduledTaskSchedule schedule, DateTimeOffset intendedFireTimeUtc) => schedule.Timing switch
    {
        ScheduledTaskTiming.OneTime => schedule.StartsAtUtc == intendedFireTimeUtc,
        ScheduledTaskTiming.Cron => new CronExpression(schedule.Expression) { TimeZone = ResolveTimeZone(schedule.TimeZoneId) }.IsSatisfiedBy(intendedFireTimeUtc),
        _ => false
    };
    /// <summary>Returns bounded preview occurrences using the same parser and zone as installation.</summary>
    public static ScheduledTaskSchedulePreview Preview(ScheduledTaskSchedule schedule, DateTimeOffset now, int count = 5)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(schedule.TaskKey) || string.IsNullOrWhiteSpace(schedule.Name) || string.IsNullOrWhiteSpace(schedule.HostId) || string.IsNullOrWhiteSpace(schedule.Environment)) errors.Add("Task, name, host and environment are required.");
        if (schedule.MaximumRuntimeSeconds is < 1 or > 86400 || schedule.DispatchToleranceSeconds is < 1 or > 3600) errors.Add("Runtime must be 1..86400 seconds and dispatch tolerance 1..3600 seconds.");
        if (schedule.EndsAtUtc <= schedule.StartsAtUtc) errors.Add("End must be after start.");
        var occurrences = new List<DateTimeOffset>();
        try
        {
            var zone = ResolveTimeZone(schedule.TimeZoneId);
            if (schedule.Timing == ScheduledTaskTiming.OneTime)
            {
                if (schedule.StartsAtUtc is not { } start || start <= now) errors.Add("A future one-time start is required.");
                else occurrences.Add(start.ToUniversalTime());
            }
            else if (schedule.Timing == ScheduledTaskTiming.Cron)
            {
                var fields = schedule.Expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (fields is not (6 or 7)) errors.Add("Use six-field Quartz cron (optional seventh year); five-field POSIX cron is not supported.");
                else
                {
                    var cron = new CronExpression(schedule.Expression) { TimeZone = zone };
                    var cursor = schedule.StartsAtUtc is { } from && from > now ? from.AddTicks(-1) : now;
                    for (var i = 0; i < Math.Clamp(count, 1, 20); i++)
                    {
                        var next = cron.GetNextValidTimeAfter(cursor);
                        if (next is null || schedule.EndsAtUtc is { } end && next > end) break;
                        occurrences.Add(next.Value); cursor = next.Value;
                    }
                    if (occurrences.Count == 0) errors.Add("No future occurrence exists within the configured bounds.");
                }
            }
            else errors.Add("Unknown timing mode.");
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { errors.Add(exception.Message); }
        return new() { Valid = errors.Count == 0, Errors = errors.ToArray(), NextFireTimesUtc = occurrences.ToArray(), TimeZoneId = schedule.TimeZoneId };
    }
}
