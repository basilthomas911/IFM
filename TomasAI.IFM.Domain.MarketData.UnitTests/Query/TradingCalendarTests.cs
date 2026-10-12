using System;
using System.Threading;
using TomasAI.IFM.Domain.MarketData.Shared.Calendar;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.Query;

public sealed class TradingCalendarTests
{
    [Fact]
    public void Inclusive_range_excludes_weekends_and_duplicate_holidays()
    {
        var start = new DateOnly(2026, 10, 9);
        var end = new DateOnly(2026, 10, 13);
        var holiday = new MarketHolidayReadModel { HolidayDate = new DateOnly(2026, 10, 12) };
        var dates = TradingCalendar.Dates(start, end, new[] { holiday, holiday });
        Assert.Equal(new[] { start, end }, dates);
        Assert.Equal(dates.Length, TradingCalendar.Count(start, end, new[] { holiday, holiday }));
    }

    [Fact]
    public void Changed_holiday_data_is_observed_without_stale_process_cache()
    {
        var date = new DateOnly(2026, 10, 12);
        Assert.Equal(1, TradingCalendar.Count(date, date, Array.Empty<MarketHolidayReadModel>()));
        Assert.Equal(0, TradingCalendar.Count(date, date, new[] { new MarketHolidayReadModel { HolidayDate = date } }));
    }

    [Fact]
    public void Inverted_and_maximum_date_ranges_do_not_overflow()
    {
        Assert.Empty(TradingCalendar.Dates(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 9), Array.Empty<MarketHolidayReadModel>()));
        Assert.Single(TradingCalendar.Dates(DateOnly.MaxValue, DateOnly.MaxValue, Array.Empty<MarketHolidayReadModel>()));
    }

    [Fact]
    public void Cancellation_interrupts_calendar_calculation()
    {
        Assert.Throws<OperationCanceledException>(() => TradingCalendar.Dates(new DateOnly(2026, 10, 9),
            new DateOnly(2026, 10, 13), Array.Empty<MarketHolidayReadModel>(), new CancellationToken(true)));
    }
}
