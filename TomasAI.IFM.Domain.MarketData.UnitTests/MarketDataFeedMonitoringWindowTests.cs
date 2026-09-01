using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class MarketDataFeedMonitoringWindowTests
{
    [Theory]
    [InlineData("2026-08-21T06:59:59Z", FuturesMarketState.OffTrading, false)]
    [InlineData("2026-08-21T07:00:00Z", FuturesMarketState.LiveTrading, true)]
    [InlineData("2026-08-21T19:59:59Z", FuturesMarketState.LiveTrading, true)]
    [InlineData("2026-08-21T20:00:00Z", FuturesMarketState.OffTrading, false)]
    [InlineData("2026-08-21T21:00:00Z", FuturesMarketState.Closed, false)]
    [InlineData("2026-08-22T14:00:00Z", FuturesMarketState.Closed, false)]
    [InlineData("2026-08-23T21:59:59Z", FuturesMarketState.Closed, false)]
    [InlineData("2026-08-23T22:00:00Z", FuturesMarketState.OffTrading, false)]
    [InlineData("2026-01-12T08:00:00Z", FuturesMarketState.LiveTrading, true)]
    public void ResolvesSessionAwareMonitoringState(
        string utcTimestamp,
        FuturesMarketState expectedState,
        bool expectedLiveMonitoring)
    {
        var instant = DateTimeOffset.Parse(utcTimestamp);

        MarketDataFeedMonitoringWindow.GetState(instant).Should().Be(expectedState);
        MarketDataFeedMonitoringWindow.IsOpen(instant).Should().Be(expectedLiveMonitoring);
    }

    [Fact]
    public void CurrentStartUsesLiveTradingBoundaryAndDaylightSavingOffset()
    {
        MarketDataFeedMonitoringWindow.GetCurrentStartUtc(
                DateTimeOffset.Parse("2026-08-21T14:00:00Z"))
            .Should().Be(DateTimeOffset.Parse("2026-08-21T07:00:00Z"));
        MarketDataFeedMonitoringWindow.GetCurrentStartUtc(
                DateTimeOffset.Parse("2026-01-12T15:00:00Z"))
            .Should().Be(DateTimeOffset.Parse("2026-01-12T08:00:00Z"));
        MarketDataFeedMonitoringWindow.GetCurrentStartUtc(
                DateTimeOffset.Parse("2026-08-21T06:59:59Z"))
            .Should().BeNull();
    }

    [Theory]
    [InlineData("2026-08-21T06:00:00Z", "2026-08-21T07:00:00Z")]
    [InlineData("2026-08-21T20:00:00Z", "2026-08-24T07:00:00Z")]
    [InlineData("2026-08-22T16:00:00Z", "2026-08-24T07:00:00Z")]
    [InlineData("2026-01-09T21:00:00Z", "2026-01-12T08:00:00Z")]
    public void NextStartUsesNextWeekdayThreeAmEastern(
        string utcTimestamp,
        string expectedUtcTimestamp)
        => MarketDataFeedMonitoringWindow.GetNextStartUtc(DateTimeOffset.Parse(utcTimestamp))
            .Should().Be(DateTimeOffset.Parse(expectedUtcTimestamp));
}
