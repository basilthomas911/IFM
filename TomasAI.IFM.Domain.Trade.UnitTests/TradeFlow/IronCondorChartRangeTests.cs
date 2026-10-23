using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using Xunit;
namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
public sealed class IronCondorChartRangeTests
{
    [Theory]
    [InlineData("2026-10-22T22:30:00Z",2026,10,23,"2026-10-22T22:30:00Z",22,23)]
    [InlineData("2026-10-22T22:30:00Z",2026,10,22,"2026-10-22T21:00:00Z",22,22)]
    [InlineData("2008-10-09T22:30:00Z",2008,10,10,"2008-10-09T22:30:00Z",9,10)]
    public void Six_hour_window_uses_server_clock_and_spans_trading_sessions(string now,int year,int month,int day,string expectedEnd,int first,int last)
    {
        var window = GetIronCondorTradePlanChart.ResolveWindow(new(year,month,day),DateTimeOffset.Parse(now));
        Assert.Equal(DateTimeOffset.Parse(expectedEnd).UtcDateTime,window.EndUtc);
        Assert.Equal(window.EndUtc.AddHours(-6),window.StartUtc);
        Assert.Equal(new DateOnly(year,month,first),window.FirstDate);
        Assert.Equal(new DateOnly(year,month,last),window.LastDate);
    }

    [Fact]
    public void Price_only_series_roundtrips_without_plan_payloads()
    {
        var start=new DateTime(2026,10,22,16,30,0,DateTimeKind.Utc);
        var series=new StrategyTradePlanChartSeries(start,start.AddHours(6),[new(start,-10m){ForwardSpread=-11m}]);
        var copy=MessagePackSerializer.Deserialize<StrategyTradePlanChartSeries>(MessagePackSerializer.Serialize(series));
        Assert.Equal(series.StartUtc,copy.StartUtc);Assert.Equal(series.EndUtc,copy.EndUtc);
        Assert.Equal(series.Points,copy.Points);
        var query=new GetIronCondorTradePlanChartQuery();
        Assert.Equal(query.ErrorCode,MessagePackSerializer.Deserialize<GetIronCondorTradePlanChartQuery>(MessagePackSerializer.Serialize(query)).ErrorCode);
    }
}
