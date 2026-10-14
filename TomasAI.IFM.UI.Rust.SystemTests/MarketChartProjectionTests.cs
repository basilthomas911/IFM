using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class MarketChartProjectionTests
{
    [Fact]
    public void Bollinger_chart_merges_current_daily_point_and_keeps_latest_valid_forty_dates()
    {
        var date=new DateOnly(2026,10,14);
        FuturesBbSignalReadModel Point(DateOnly day, decimal price, TimeFrameType period=TimeFrameType.Daily, bool valid=true) => new() {
            Price=price,Ema20Center=price-1,Upper20=price+5,Lower20=price-5,
            Metadata=new() { ValueDate=day,IsValid=valid,MarketDataAsOfUtc=new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc)),
                SignalKey=new(default,default,period,"chart-test") } };
        var history=Enumerable.Range(1,45).Select(offset=>Point(date.AddDays(-offset),7800m)).ToArray();
        var current=Point(date,7900m);
        var newer=current with { Price=7901m,Metadata=current.Metadata with { MarketDataAsOfUtc=current.Metadata.MarketDataAsOfUtc.AddHours(1) } };
        var chart=MarketChartProjection.Bands(date,[.. history,current,newer,Point(date.AddDays(1),9999m),Point(date,9999m,TimeFrameType.OneMinute),Point(date,9999m,valid:false)]);
        Assert.Equal(40,chart.Series[0].Points.Length);
        Assert.Equal(7901d,chart.Series[0].Points[^1][1]);
        Assert.Equal(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc)).ToUnixTimeSeconds(),chart.Series[0].Points[^1][0]);
    }

    [Fact]
    public void Six_hour_bars_span_value_dates_and_use_ET_and_WinForms_bounds()
    {
        var end = new DateTime(2026,10,13,2,0,0,DateTimeKind.Utc);var start=end.AddHours(-6);
        var bars = new[] { new FuturesBarDataReadModel {ContractId="ES",Symbol="ES",ValueDate=new(2026,10,12),BarDate=start.AddHours(1),BarRateType=BarRateType.FifteenSeconds,BarValue=7800,UpTrendTrigger=7820,DownTrendTrigger=7790}, new FuturesBarDataReadModel {ContractId="ES",Symbol="ES",ValueDate=new(2026,10,13),BarDate=end.AddMinutes(-1),BarRateType=BarRateType.FifteenSeconds,BarValue=7810,UpTrendTrigger=7820,DownTrendTrigger=7790} };
        var chart=MarketChartProjection.Bars("ES",start,end,bars);
        Assert.Equal(2,chart.Series[0].Points.Length);
        Assert.Equal(7785,chart.Minimum);Assert.Equal(7825,chart.Maximum);
        Assert.Equal("Yellow",chart.Series[0].Color);Assert.Equal("Lime",chart.Series[1].Color);
        Assert.Equal(StrategyDetailsAdapter.MarketSeconds(start),chart.Start);
        var single=MarketChartProjection.Bars("VX",start,end,bars.Take(1));
        Assert.Equal(2,single.Series[0].Points.Length);Assert.Equal(single.End,single.Series[0].Points[1][0]);
        Assert.Equal("Fuchsia",single.Series[0].Color);
    }
}
