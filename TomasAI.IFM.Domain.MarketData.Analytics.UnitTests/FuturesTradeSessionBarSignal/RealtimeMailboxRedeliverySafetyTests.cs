using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarSignal;

public sealed class RealtimeMailboxRedeliverySafetyTests
{
    [Fact]
    public async Task Bar_handler_retains_failed_handoffs_and_restores_acknowledged_open_state()
        => Assert.NotNull(await RealtimeMailboxSafetyFixture.Verify());
}
