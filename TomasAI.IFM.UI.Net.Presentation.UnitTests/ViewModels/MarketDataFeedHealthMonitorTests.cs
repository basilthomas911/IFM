using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

[Trait("TestType", "BDD")]
public class MarketDataFeedHealthMonitorTests
{
    static readonly DateTimeOffset LiveStart =
        new(2026, 8, 21, 7, 0, 0, TimeSpan.Zero); // Friday 03:00 Eastern

    [Fact]
    public void LiveTradingUsesExactFiveAndFifteenMinuteBoundaries()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        monitor.Activate(["ESU6", "VXU6"], LiveStart, FuturesMarketState.LiveTrading).State
            .Should().Be(MarketDataFeedHealthState.Healthy);

        monitor.Evaluate(LiveStart.AddMinutes(5)).State
            .Should().Be(MarketDataFeedHealthState.Healthy);
        monitor.Evaluate(LiveStart.AddMinutes(5).AddTicks(1)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);
        monitor.Evaluate(LiveStart.AddMinutes(15)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);

        var critical = monitor.Evaluate(LiveStart.AddMinutes(15).AddTicks(1));
        critical.State.Should().Be(MarketDataFeedHealthState.Critical);
        critical.EnteredCritical.Should().BeTrue();
        critical.StaleContractIds.Should().BeEquivalentTo("ESU6", "VXU6");
        monitor.Evaluate(LiveStart.AddMinutes(16)).EnteredCritical.Should().BeFalse();
    }

    [Fact]
    public void OffTradingDegradesAfterFifteenMinutesWithoutStoppingOrEnteringCritical()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        var offHoursStart = DateTimeOffset.Parse("2026-08-23T22:00:00Z");

        monitor.Activate(["ESU6"], offHoursStart, FuturesMarketState.OffTrading).State
            .Should().Be(MarketDataFeedHealthState.OffHoursActive);
        monitor.Evaluate(offHoursStart.AddMinutes(15)).State
            .Should().Be(MarketDataFeedHealthState.OffHoursActive);
        var degraded = monitor.Evaluate(offHoursStart.AddMinutes(15).AddTicks(1));

        degraded.State.Should().Be(MarketDataFeedHealthState.OffHoursDegraded);
        degraded.EnteredCritical.Should().BeFalse();
        degraded.StaleContractIds.Should().ContainSingle().Which.Should().Be("ESU6");
    }

    [Fact]
    public void DegradedOvernightRouteStartsNewGreenEpochAtThreeAm()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        var offHoursStart = LiveStart.AddHours(-9);
        monitor.Activate(["ESU6"], offHoursStart, FuturesMarketState.OffTrading);
        monitor.Evaluate(LiveStart.AddTicks(-1)).State
            .Should().Be(MarketDataFeedHealthState.OffHoursDegraded);

        monitor.SetMarketState(FuturesMarketState.LiveTrading, LiveStart).State
            .Should().Be(MarketDataFeedHealthState.Healthy);
        monitor.Evaluate(LiveStart.AddMinutes(5).AddTicks(1)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);
        monitor.Evaluate(LiveStart.AddMinutes(15).AddTicks(1)).State
            .Should().Be(MarketDataFeedHealthState.Critical);
    }

    [Fact]
    public void SixteenHundredReclassifiesWithoutCriticalAlertAndKeepsTimestamp()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        monitor.Activate(["ESU6"], LiveStart, FuturesMarketState.LiveTrading);
        var fourPm = DateTimeOffset.Parse("2026-08-21T20:00:00Z");
        monitor.Evaluate(fourPm.AddTicks(-1)).State.Should().Be(MarketDataFeedHealthState.Critical);

        var offHours = monitor.SetMarketState(FuturesMarketState.OffTrading, fourPm);

        offHours.State.Should().Be(MarketDataFeedHealthState.OffHoursDegraded);
        offHours.EnteredCritical.Should().BeFalse();
    }

    [Fact]
    public void EachCurrentContractRecoversIndependently()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        monitor.Activate(["ESU6", "VXU6"], LiveStart, FuturesMarketState.LiveTrading);
        monitor.Evaluate(LiveStart.AddMinutes(16));

        monitor.RecordUpdate("ESU6", LiveStart.AddMinutes(16)).State
            .Should().Be(MarketDataFeedHealthState.Critical, "VX is still stale");
        monitor.RecordUpdate("VXU6", LiveStart.AddMinutes(16)).State
            .Should().Be(MarketDataFeedHealthState.Healthy);
    }

    [Fact]
    public void DelayedDatabentoBacklogCannotRestoreGreen()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        var now = LiveStart.AddMinutes(20);
        monitor.Activate(["ESU6"], LiveStart, FuturesMarketState.LiveTrading);

        var snapshot = monitor.RecordUpdate(
            "ESU6",
            now,
            sourceEventUtc: LiveStart.AddMinutes(1));

        snapshot.State.Should().Be(MarketDataFeedHealthState.Critical);
    }

    [Fact]
    public void ClosedStateIsInactiveEvenBeforeFeedLifecycleStopCompletes()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        monitor.Activate(["ESU6"], LiveStart, FuturesMarketState.LiveTrading);

        monitor.SetMarketState(FuturesMarketState.Closed, LiveStart.AddHours(14)).State
            .Should().Be(MarketDataFeedHealthState.Inactive);
    }
}
