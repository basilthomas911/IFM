using FluentAssertions;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class MarketDataFeedHealthMonitorTests
{
    static readonly DateTimeOffset FridayTenAmEastern =
        new(2026, 8, 21, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_UsesExactFiveAndFifteenMinuteBoundaries()
    {
        var monitor = new MarketDataFeedHealthMonitor();

        monitor.Activate(["ESU6", "VXU6"], FridayTenAmEastern).State
            .Should().Be(MarketDataFeedHealthState.Healthy);
        monitor.RecordUpdate("ESU6", FridayTenAmEastern);
        monitor.RecordUpdate("VXU6", FridayTenAmEastern);

        monitor.Evaluate(FridayTenAmEastern.AddMinutes(5)).State
            .Should().Be(MarketDataFeedHealthState.Healthy);
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(5).AddSeconds(1)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(15)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);

        var critical = monitor.Evaluate(FridayTenAmEastern.AddMinutes(15).AddSeconds(1));
        critical.State.Should().Be(MarketDataFeedHealthState.Critical);
        critical.EnteredCritical.Should().BeTrue();
        critical.StaleContractIds.Should().BeEquivalentTo("ESU6", "VXU6");
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(16)).EnteredCritical.Should().BeFalse();
    }

    [Fact]
    public void RecordUpdate_RequiresEveryCurrentContractAndResetsARecoveredEpisode()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        monitor.Activate(["ESU6", "VXU6"], FridayTenAmEastern);
        monitor.RecordUpdate("ESU6", FridayTenAmEastern);
        monitor.RecordUpdate("VXU6", FridayTenAmEastern);
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(16));

        monitor.RecordUpdate("ESU6", FridayTenAmEastern.AddMinutes(16)).State
            .Should().Be(MarketDataFeedHealthState.Critical, "VX is still stale");
        var recovered = monitor.RecordUpdate("VXU6", FridayTenAmEastern.AddMinutes(16));
        recovered.State.Should().Be(MarketDataFeedHealthState.Healthy);

        monitor.Evaluate(FridayTenAmEastern.AddMinutes(32)).EnteredCritical.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_RemainsActiveOutsidePositionEntryHours()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        var fridayFivePmEastern = new DateTimeOffset(2026, 8, 21, 21, 0, 0, TimeSpan.Zero);
        monitor.Activate(["ESU6", "VXU6"], fridayFivePmEastern).State
            .Should().Be(MarketDataFeedHealthState.Healthy);

        monitor.Evaluate(fridayFivePmEastern.AddMinutes(6)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);
        monitor.Evaluate(fridayFivePmEastern.AddMinutes(16)).State
            .Should().Be(MarketDataFeedHealthState.Critical);
    }

    [Fact]
    public void RecordUpdate_DelayedDatabentoBacklogCannotRestoreGreen()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        var now = FridayTenAmEastern.AddMinutes(20);
        monitor.Activate(["ESU6"], FridayTenAmEastern);

        var snapshot = monitor.RecordUpdate(
            "ESU6",
            now,
            sourceEventUtc: FridayTenAmEastern.AddMinutes(1));

        snapshot.State.Should().Be(MarketDataFeedHealthState.Critical);
        snapshot.StaleContractIds.Should().ContainSingle().Which.Should().Be("ESU6");
    }
}
