using FluentAssertions;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class MarketDataFeedHealthMonitorTests
{
    static readonly DateTimeOffset FridayTenAmEastern =
        new(2026, 8, 21, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_EscalatesOnlyAfterEachContinuousStage()
    {
        var monitor = new MarketDataFeedHealthMonitor();

        monitor.Activate(["ESU6", "VXU6"], FridayTenAmEastern).State
            .Should().Be(MarketDataFeedHealthState.Healthy);
        monitor.RecordUpdate("ESU6", FridayTenAmEastern);
        monitor.RecordUpdate("VXU6", FridayTenAmEastern);

        monitor.Evaluate(FridayTenAmEastern.AddMinutes(1)).State
            .Should().Be(MarketDataFeedHealthState.Healthy);
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(1).AddSeconds(1)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(6)).State
            .Should().Be(MarketDataFeedHealthState.Failed);

        var critical = monitor.Evaluate(FridayTenAmEastern.AddMinutes(21));
        critical.State.Should().Be(MarketDataFeedHealthState.Critical);
        critical.EnteredCritical.Should().BeTrue();
        critical.StaleContractIds.Should().BeEquivalentTo("ESU6", "VXU6");
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(22)).EnteredCritical.Should().BeFalse();
    }

    [Fact]
    public void RecordUpdate_RequiresEveryCurrentContractAndResetsARecoveredEpisode()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        monitor.Activate(["ESU6", "VXU6"], FridayTenAmEastern);
        monitor.RecordUpdate("ESU6", FridayTenAmEastern);
        monitor.RecordUpdate("VXU6", FridayTenAmEastern);
        monitor.Evaluate(FridayTenAmEastern.AddMinutes(21));

        monitor.RecordUpdate("ESU6", FridayTenAmEastern.AddMinutes(21)).State
            .Should().Be(MarketDataFeedHealthState.Critical, "VX is still stale");
        var recovered = monitor.RecordUpdate("VXU6", FridayTenAmEastern.AddMinutes(21));
        recovered.State.Should().Be(MarketDataFeedHealthState.Healthy);

        monitor.Evaluate(FridayTenAmEastern.AddMinutes(42)).EnteredCritical.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_SuppressesWarningsOutsideEntryHoursAndStartsWithFreshGracePeriod()
    {
        var monitor = new MarketDataFeedHealthMonitor();
        var fridayFivePmEastern = new DateTimeOffset(2026, 8, 21, 21, 0, 0, TimeSpan.Zero);
        monitor.Activate(["ESU6", "VXU6"], fridayFivePmEastern).State
            .Should().Be(MarketDataFeedHealthState.OutsidePositionEntryWindow);

        monitor.Evaluate(fridayFivePmEastern.AddHours(24)).State
            .Should().Be(MarketDataFeedHealthState.OutsidePositionEntryWindow);

        var mondayThreeAmEastern = new DateTimeOffset(2026, 8, 24, 7, 0, 0, TimeSpan.Zero);
        monitor.Evaluate(mondayThreeAmEastern).State.Should().Be(MarketDataFeedHealthState.Healthy);
        monitor.Evaluate(mondayThreeAmEastern.AddMinutes(1).AddSeconds(1)).State
            .Should().Be(MarketDataFeedHealthState.Intermittent);
    }
}
