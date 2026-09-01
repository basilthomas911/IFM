using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

[Trait("TestType", "BDD")]
public sealed class MarketDataFeedSessionHealthPolicyTests
{
    static readonly DateTimeOffset LiveStart =
        DateTimeOffset.Parse("2026-08-21T07:00:00Z");

    [Theory]
    [InlineData(0, MarketDataFeedSessionHealthState.Green)]
    [InlineData(5, MarketDataFeedSessionHealthState.Green)]
    [InlineData(6, MarketDataFeedSessionHealthState.Yellow)]
    [InlineData(15, MarketDataFeedSessionHealthState.Yellow)]
    [InlineData(16, MarketDataFeedSessionHealthState.Red)]
    public void LiveTradingUsesFiveAndFifteenMinuteAcceptedCacheBoundaries(
        int elapsedMinutes,
        MarketDataFeedSessionHealthState expected)
        => MarketDataFeedSessionHealthPolicy.Evaluate(
                FuturesMarketState.LiveTrading,
                LiveStart.AddMinutes(elapsedMinutes),
                LiveStart.AddHours(-9),
                null,
                routeActive: true,
                routeConfiguredAndRunning: true)
            .Should().Be(expected);

    [Fact]
    public void OvernightDegradationBecomesGreenAtThreeAmWithoutRestartingTheRoute()
    {
        var activation = LiveStart.AddHours(-9);
        MarketDataFeedSessionHealthPolicy.Evaluate(
                FuturesMarketState.OffTrading,
                LiveStart.AddTicks(-1),
                activation,
                activation,
                true,
                true)
            .Should().Be(MarketDataFeedSessionHealthState.OffHoursDegraded);

        MarketDataFeedSessionHealthPolicy.Evaluate(
                FuturesMarketState.LiveTrading,
                LiveStart,
                activation,
                activation,
                true,
                true)
            .Should().Be(MarketDataFeedSessionHealthState.Green);
    }

    [Theory]
    [InlineData(15, MarketDataFeedSessionHealthState.OffHoursActive)]
    [InlineData(16, MarketDataFeedSessionHealthState.OffHoursDegraded)]
    public void OffTradingUsesOneNonCriticalFifteenMinuteBoundary(
        int elapsedMinutes,
        MarketDataFeedSessionHealthState expected)
        => MarketDataFeedSessionHealthPolicy.Evaluate(
                FuturesMarketState.OffTrading,
                LiveStart.AddMinutes(elapsedMinutes),
                LiveStart,
                null,
                true,
                true)
            .Should().Be(expected);

    [Fact]
    public void ClosedOrUnownedRouteIsInactive()
    {
        MarketDataFeedSessionHealthPolicy.Evaluate(
                FuturesMarketState.Closed, LiveStart, LiveStart, null, true, true)
            .Should().Be(MarketDataFeedSessionHealthState.Inactive);
        MarketDataFeedSessionHealthPolicy.Evaluate(
                FuturesMarketState.LiveTrading, LiveStart, LiveStart, null, false, true)
            .Should().Be(MarketDataFeedSessionHealthState.Inactive);
    }
}
