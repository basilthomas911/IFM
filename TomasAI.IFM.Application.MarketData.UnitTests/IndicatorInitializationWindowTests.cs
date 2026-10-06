using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class IndicatorInitializationWindowTests
{
    [Fact]
    public void After_daily_maintenance_seed_contains_48_real_bars_and_assigns_post_break_trades_correctly()
    {
        var cutoff = new DateTimeOffset(2026, 10, 5, 22, 15, 0, TimeSpan.Zero);
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes, 48, cutoff, new CmeFuturesMarketSessionCalendar());
        Assert.Equal(48, windows.Length);
        Assert.True(windows[0].StartUtc < cutoff.AddHours(-4));
        Assert.Equal(cutoff, windows[^1].EndUtc);
        Assert.Equal(-1, RsiHistoricalSeedWindowModel.FindIntervalIndex(windows, new(2026, 10, 5, 21, 30, 0, TimeSpan.Zero)));
        Assert.Equal(45, RsiHistoricalSeedWindowModel.FindIntervalIndex(windows, new(2026, 10, 5, 22, 0, 0, TimeSpan.Zero)));
        Assert.Equal(47, RsiHistoricalSeedWindowModel.FindIntervalIndex(windows, cutoff.AddMinutes(-1)));
        Assert.Equal(-1, RsiHistoricalSeedWindowModel.FindIntervalIndex(windows, cutoff));
    }

    [Fact]
    public void During_maintenance_seed_uses_completed_prior_session_bars()
    {
        var cutoff = new DateTimeOffset(2026, 10, 5, 21, 30, 0, TimeSpan.Zero);
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes, 48, cutoff, new CmeFuturesMarketSessionCalendar());
        Assert.Equal(48, windows.Length);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 21, 0, 0, TimeSpan.Zero), windows[^1].EndUtc);
        Assert.Equal(47, RsiHistoricalSeedWindowModel.FindIntervalIndex(windows, windows[^1].EndUtc.AddSeconds(-1)));
    }
}
