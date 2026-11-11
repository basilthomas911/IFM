using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Databento.Historical;

/// <summary>Measures market observation age and expiry using open exchange sessions.</summary>
public static class MarketTradingTime
{
    /// <summary>Counts only open-session time, preserving negative ages for future-timestamp validation.</summary>
    public static TimeSpan Elapsed(IMarketSessionCalendar calendar, DateTimeOffset from, DateTimeOffset through,
        TimeSpan? stopAfter = null)
    {
        if (through <= from) return through - from;
        var first = calendar.GetValueDate(from.ToUniversalTime());
        var last = calendar.GetValueDate(through.ToUniversalTime());
        var age = TimeSpan.Zero;
        for (var date = first; ; date = date.AddDays(1))
        {
            if (calendar.IsTradingDate(date))
            {
                var session = calendar.GetSession(date);
                var start = session.StartUtc > from ? session.StartUtc : from;
                var end = session.EndUtc < through ? session.EndUtc : through;
                if (end > start) age += end - start;
                if (stopAfter is { } limit && age > limit) return age;
            }
            if (date == last) return age;
        }
    }

    /// <summary>Moves a market timestamp forward or backward by open-session time, skipping closures.</summary>
    public static DateTimeOffset Add(IMarketSessionCalendar calendar, DateTimeOffset at, TimeSpan duration)
    {
        if (duration == TimeSpan.Zero) return at;
        var forward = duration > TimeSpan.Zero;
        var remaining = duration.Duration();
        var date = calendar.GetValueDate(at.ToUniversalTime());
        var cursor = at;
        while (true)
        {
            if (calendar.IsTradingDate(date))
            {
                var session = calendar.GetSession(date);
                if (forward)
                {
                    if (cursor < session.StartUtc) cursor = session.StartUtc;
                    if (cursor < session.EndUtc)
                    {
                        var available = session.EndUtc - cursor;
                        if (remaining <= available) return cursor + remaining;
                        remaining -= available;
                    }
                }
                else
                {
                    if (cursor > session.EndUtc) cursor = session.EndUtc;
                    if (cursor > session.StartUtc)
                    {
                        var available = cursor - session.StartUtc;
                        if (remaining <= available) return cursor - remaining;
                        remaining -= available;
                    }
                }
            }
            date = date.AddDays(forward ? 1 : -1);
        }
    }
}
