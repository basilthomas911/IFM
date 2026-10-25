using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Display parity oracle, invoking the unchanged hybrid chart projection.</summary>
internal static class MarketHistoryFixtures
{
    public static void Export(string directory)
    {
        var end=new DateTime(2026,11,1,8,30,0,DateTimeKind.Utc); // DST transition in the window.
        var start=end.AddHours(-6);var date=DateOnly.FromDateTime(end);
        FuturesBarDataReadModel Bar(DateTime time,decimal value,BarRateType rate=BarRateType.FifteenSeconds)=>new("ESZ6","ES",date,time,rate,value,6100,6000);
        var samples=new[]{Array.Empty<FuturesBarDataReadModel>(),new[]{Bar(end.AddSeconds(-15),6050.125m)},new[]{Bar(end,6060),Bar(start,6040),Bar(start.AddSeconds(-1),9999),Bar(start.AddHours(2),9000,BarRateType.Minute)}};
        var options=new JsonSerializerOptions{WriteIndented=true};options.Converters.Add(new JsonStringEnumConverter());
        var cases=new List<object>();
        var displayOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach(var symbol in new[]{"ES","VX"})foreach(var bars in samples)
            cases.Add(new{symbol,start,end,source=bars,expected=JsonSerializer.SerializeToElement(MarketChartProjection.Bars(symbol,start,end,bars),displayOptions)});
        FixtureFiles.WriteAllText(Path.Combine(directory,"market-bars-display.json"),JsonSerializer.Serialize(cases,options));
        FuturesBbSignalReadModel Band(int days,decimal price,bool valid=true,decimal? center=6050)=>new(){
            Metadata=new(){ValueDate=date.AddDays(days),IsValid=valid,MarketDataAsOfUtc=new DateTimeOffset(end.AddDays(days)),SignalKey=new(){TimeFrame=TimeFrameType.Daily}},
            Price=price,Ema20Center=center,Upper20=center+10,Lower20=center-10};
        var sources=new[]{Array.Empty<FuturesBbSignalReadModel>(),new[]{Band(0,6050,center:null)},
            Enumerable.Range(-45,46).Select(day=>Band(day,6050+day)).Concat(new[]{Band(1,9000),Band(-1,9999,false),Band(0,6060) with{Metadata=Band(0,6060).Metadata with{MarketDataAsOfUtc=new DateTimeOffset(end.AddSeconds(1))}}}).ToArray()};
        FixtureFiles.WriteAllText(Path.Combine(directory,"market-bands-display.json"),JsonSerializer.Serialize(sources.Select(source=>new{date,source,expected=JsonSerializer.SerializeToElement(MarketChartProjection.Bands(date,source),displayOptions)}),options));
    }
}
