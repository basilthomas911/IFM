using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

/// <summary>Server-owned exchange partition enumeration for bounded timestamp queries.</summary>
public static class FuturesBarWindowPartitions
{
    public static IReadOnlyCollection<DateOnly> Get(DateOnly valueDate, DateTime startUtc, DateTime endUtc)
    {
        if (valueDate == default || startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc
            || endUtc < startUtc || endUtc - startUtc > TimeSpan.FromDays(366))
            throw new ArgumentException("A UTC history window of at most 366 days and an operational ValueDate are required.");
        var zone = FuturesTradingValueDate.MarketTimeZone;
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startUtc, zone));
        var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(endUtc, zone));
        var dates = new SortedSet<DateOnly> { valueDate };
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            dates.Add(date);
            var opensUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(18, 0)), zone);
            if (opensUtc <= endUtc && date.DayOfWeek is >= DayOfWeek.Sunday and <= DayOfWeek.Thursday)
                dates.Add(date.AddDays(1));
        }
        return dates;
    }
}
