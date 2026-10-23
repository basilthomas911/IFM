using TomasAI.IFM.UI.Net.Models.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public sealed class ScheduleTimeTests
{
    [Theory]
    [InlineData("America/Toronto", -4)]
    [InlineData("Europe/London", 1)]
    [InlineData("UTC", 0)]
    public void Explicit_schedule_zone_round_trips_independently_of_workstation(string zone, int offset)
    {
        var instant = new DateTimeOffset(2026, 7, 15, 16, 0, 0, TimeSpan.Zero);
        var display = ScheduleTime.FromUtc(instant, zone);
        Assert.Equal(TimeSpan.FromHours(offset), display.Offset);
        Assert.Equal(instant, ScheduleTime.ToUtc(display.DateTime, zone));
    }

    [Theory]
    [InlineData(3, 8, 2, 30)]
    [InlineData(11, 1, 1, 30)]
    public void Missing_or_ambiguous_dst_bound_is_rejected(int month, int day, int hour, int minute)
        => Assert.Throws<ArgumentException>(() => ScheduleTime.ToUtc(new(2026, month, day, hour, minute, 0), "America/Toronto"));
}
