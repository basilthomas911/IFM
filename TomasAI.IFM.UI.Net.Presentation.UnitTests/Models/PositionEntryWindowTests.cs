using FluentAssertions;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public class PositionEntryWindowTests
{
    [Theory]
    [InlineData(2026, 8, 21, 6, 59, false)] // 02:59 EDT
    [InlineData(2026, 8, 21, 7, 0, true)]   // 03:00 EDT
    [InlineData(2026, 8, 21, 19, 59, true)] // 15:59 EDT
    [InlineData(2026, 8, 21, 20, 0, false)] // 16:00 EDT
    [InlineData(2026, 8, 22, 14, 0, false)] // Saturday
    public void IsOpen_UsesWeekdayEasternBoundaries(
        int year,
        int month,
        int day,
        int hourUtc,
        int minuteUtc,
        bool expected)
    {
        var utcNow = new DateTimeOffset(year, month, day, hourUtc, minuteUtc, 0, TimeSpan.Zero);

        PositionEntryWindow.IsOpen(utcNow).Should().Be(expected);
    }

    [Fact]
    public void IsOpen_AppliesEasternDaylightSavingRules()
    {
        // 08:00 UTC is 03:00 EST in January, but 04:00 EDT in August.
        PositionEntryWindow.IsOpen(new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        PositionEntryWindow.GetCurrentStartUtc(new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero));
        PositionEntryWindow.GetCurrentStartUtc(new DateTimeOffset(2026, 8, 21, 14, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 8, 21, 7, 0, 0, TimeSpan.Zero));
    }
}
