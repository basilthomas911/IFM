using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public sealed class TradeMonitoringPolicyTests
{
    [Fact]
    public void Only_open_and_closed_trades_are_loadable()
    {
        foreach (var state in Enum.GetValues<TradeState>())
            TradeMonitoringPolicy.CanLoad(state).Should().Be(state is TradeState.Open or TradeState.Closed);
    }

    [Theory]
    [InlineData("Production", BrokerEnvironment.Live, "2026-10-09T20:59:00Z", true)]
    [InlineData("Production", BrokerEnvironment.Live, "2026-10-09T21:00:00Z", false)]
    [InlineData("Production", BrokerEnvironment.Emulator, "2026-10-10T12:00:00Z", false)]
    [InlineData("Production", BrokerEnvironment.Live, "2026-10-11T22:00:00Z", true)]
    [InlineData("Production", BrokerEnvironment.Live, "2026-10-08T21:30:00Z", false)]
    [InlineData("Development", BrokerEnvironment.Emulator, "2026-10-10T12:00:00Z", true)]
    [InlineData("Development", BrokerEnvironment.Live, "2026-10-09T15:00:00Z", false)]
    [InlineData("Development", BrokerEnvironment.Paper, "2026-10-09T15:00:00Z", false)]
    [InlineData("Staging", BrokerEnvironment.Emulator, "2026-10-09T15:00:00Z", false)]
    public void Feed_start_requires_the_configured_environment_broker_and_session(
        string environment, BrokerEnvironment broker, string instant, bool allowed)
        => TradeMonitoringPolicy.CanStart(environment, broker, DateTimeOffset.Parse(instant)).Should().Be(allowed);
}
