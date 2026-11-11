using TomasAI.IFM.Application.MarketData.Databento.Historical;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.RegimeDiscovery;

public sealed class MarketTradingTimeTests
{
    [Theory]
    [InlineData("2026-11-10T21:59:00Z", "2026-11-10T23:02:00Z")]
    [InlineData("2026-11-06T21:59:00Z", "2026-11-08T23:02:00Z")]
    [InlineData("2026-10-30T20:59:00Z", "2026-11-01T23:02:00Z")]
    public void Trading_age_and_expiry_skip_closures_in_both_directions(string first, string last)
    {
        var calendar = new CmeFuturesMarketSessionCalendar();
        var from = DateTimeOffset.Parse(first);
        var through = DateTimeOffset.Parse(last);
        var elapsed = TimeSpan.FromMinutes(3);
        Assert.Equal(elapsed, MarketTradingTime.Elapsed(calendar, from, through));
        Assert.Equal(through, MarketTradingTime.Add(calendar, from, elapsed));
        Assert.Equal(from, MarketTradingTime.Add(calendar, through, -elapsed));
    }

    [Fact]
    public void Closed_hour_does_not_consume_the_freshness_budget_but_open_time_does()
    {
        var calendar = new CmeFuturesMarketSessionCalendar();
        var from = DateTimeOffset.Parse("2026-11-10T21:59:00Z");
        Assert.Equal(TimeSpan.FromMinutes(1), MarketTradingTime.Elapsed(calendar, from, DateTimeOffset.Parse("2026-11-10T22:45:00Z")));
        Assert.Equal(TimeSpan.FromMinutes(16), MarketTradingTime.Elapsed(calendar, from, DateTimeOffset.Parse("2026-11-10T23:15:00Z")));
        Assert.Equal(TimeSpan.FromMinutes(-1), MarketTradingTime.Elapsed(calendar, from, from.AddMinutes(-1)));
    }
}
