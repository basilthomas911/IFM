using System.Collections.Immutable;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>
/// Versioned application freshness policy, independent of CME sessions. The 18:00 Eastern deadline
/// is an application allowance, not a Treasury SLA. Coverage expires rather than guessing next year's calendar.
/// </summary>
public static class UsTreasuryPublicationCalendar
{
    public static TreasuryPublicationPolicy Default2026 { get; } = Create2026();

    static TreasuryPublicationPolicy Create2026()
    {
        // Full US government securities holidays. Good Friday 2026 is an early close, not a full close.
        DateOnly[] holidays = [new(2026, 1, 1), new(2026, 1, 19), new(2026, 2, 16),
            new(2026, 5, 25), new(2026, 6, 19), new(2026, 7, 3), new(2026, 9, 7),
            new(2026, 10, 12), new(2026, 11, 11), new(2026, 11, 26), new(2026, 12, 25)];
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var first = new DateOnly(2025, 12, 31); // Seed the last required observation at the start of coverage.
        var publications = Enumerable.Range(0, 366).Select(first.AddDays)
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(d))
            .Select(d => new TreasuryPublicationDate(d,
                new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(new TimeOnly(18, 0)), eastern))))
            .ToImmutableArray();
        return new("USTreasury-2026-18ET/v1", new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), publications);
    }
}
