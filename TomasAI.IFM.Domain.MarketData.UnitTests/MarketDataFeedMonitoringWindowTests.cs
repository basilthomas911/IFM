using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class MarketDataFeedMonitoringWindowTests
{
    [Theory]
    [InlineData("2026-08-21T06:59:59Z", true)]  // Friday 02:59:59 EDT
    [InlineData("2026-08-21T07:00:00Z", true)]  // Friday 03:00 EDT
    [InlineData("2026-08-21T19:59:59Z", true)]  // Friday 15:59:59 EDT
    [InlineData("2026-08-21T20:00:00Z", true)]  // Friday 16:00 EDT
    [InlineData("2026-08-21T21:00:00Z", false)] // Friday 17:00 EDT
    [InlineData("2026-08-22T14:00:00Z", false)] // Saturday
    [InlineData("2026-08-23T21:59:59Z", false)] // Sunday 17:59:59 EDT
    [InlineData("2026-08-23T22:00:00Z", true)]  // Sunday 18:00 EDT
    [InlineData("2026-01-12T08:00:00Z", true)]  // Monday 03:00 EST
    public void IsOpenUsesEasternValueDateSession(string utcTimestamp, bool expected)
    {
        var instant = DateTimeOffset.Parse(
            utcTimestamp,
            System.Globalization.CultureInfo.InvariantCulture);

        MarketDataFeedMonitoringWindow.IsOpen(instant).Should().Be(expected);
    }

    [Fact]
    public void CurrentStartUsesDaylightSavingOffset()
    {
        MarketDataFeedMonitoringWindow.GetCurrentStartUtc(
                DateTimeOffset.Parse("2026-08-21T14:00:00Z"))
            .Should().Be(DateTimeOffset.Parse("2026-08-20T22:00:00Z"));
        MarketDataFeedMonitoringWindow.GetCurrentStartUtc(
                DateTimeOffset.Parse("2026-01-12T15:00:00Z"))
            .Should().Be(DateTimeOffset.Parse("2026-01-11T23:00:00Z"));
    }

    [Theory]
    [InlineData("2026-08-21T06:00:00Z", "2026-08-23T22:00:00Z")]
    [InlineData("2026-08-21T20:00:00Z", "2026-08-23T22:00:00Z")]
    [InlineData("2026-08-22T16:00:00Z", "2026-08-23T22:00:00Z")]
    [InlineData("2026-01-09T21:00:00Z", "2026-01-11T23:00:00Z")]
    public void NextStartSkipsClosedHoursAndWeekends(
        string utcTimestamp,
        string expectedUtcTimestamp)
    {
        MarketDataFeedMonitoringWindow.GetNextStartUtc(
                DateTimeOffset.Parse(utcTimestamp))
            .Should().Be(DateTimeOffset.Parse(expectedUtcTimestamp));
    }
}
