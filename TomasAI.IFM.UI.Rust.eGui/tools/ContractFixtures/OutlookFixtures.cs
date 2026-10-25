using System.Globalization;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Presentation-only samples, evaluated by the unchanged hybrid presenter.</summary>
internal static class OutlookFixtures
{
    public static void Export(string directory)
    {
        var date=new DateOnly(2026,10,26);var now=new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc);
        var cold=new MarketOutlookReadModel{ContractId="ESZ6",ValueDate=date,UpdatedAtUtc=now,MissingInputs="EMA, ITI"};
        var hot=cold with{MissingInputs="",FuturesEodData=new(){ContractId="ESZ6",ValueDate=date,OpenPrice=6050.125m,HighPrice=6060.225m,LowPrice=6040.335m,ClosePrice=6055.445m,Volume=123456,DailyPercentChange=0.012345,MarketDirection=MarketDirectionType.Up,PriceDirection=PriceDirectionType.Rising},
            FuturesTradeSignal=new(){ContractId="ESZ6",ValueDate=date,TrendType=FuturesTrendType.UpTrend,TrendStrength=FuturesTrendStrengthType.High,ExitTrigger=6040.125},
            FuturesEmaSignal=new(){IsWarm=true,Ema50=6040.125m,Ema200=6020.125m,BaselineValueDate=date.AddDays(-1)},
            FuturesBbSignal=new(){IsWarm=true,StandardDeviation20=10.125m,Ema20Center=6050.125m,Upper20=6070.125m,Lower20=6030.125m,BaselineValueDate=date.AddDays(-1)},
            MdiPercent=65.125m,MdiClassification=FuturesMDITrendType.UpTrending,FuturesRsiSignal=new(){RSI=65.125},
            LatestItiTrendSignal=new(){IntrinsicTimeTrend=IntrinsicTimeTrendType.DownTrend,IntrinsicPrice=6050.125,UpTrendTrigger=6060.125,DownTrendTrigger=6040.125,TrendDelta=-2.125,TrendExtreme=6055.125,TrendReversal=6049.125},
            FuturesVwapSignal=new(){IsWarm=true,IsValid=true,IsTickExact=true,Vwap=6050.125m},FuturesAdxSignal=new(){IsWarm=true,AdxValue=26,PlusDI=30,MinusDI=20},
            FuturesAtrSignal=new(){IsWarm=true,AtrValue=12.125,AtrRatio=1},FuturesMacdSignal=new(){IsWarm=true,Histogram=3.125},FuturesTdiSignal=new(){PriceSignalDivergence=-1.125}
        };
        var samples=new[]{cold,hot,hot with{FuturesTradeSignal=null,FuturesEodData=hot.FuturesEodData with{ContractId=""},FuturesEmaSignal=hot.FuturesEmaSignal! with{IsWarm=false},FuturesRsiSignal=null,MdiClassification=null,FuturesVwapSignal=hot.FuturesVwapSignal! with{IsTickExact=false},FuturesAdxSignal=hot.FuturesAdxSignal! with{PlusDI=20,MinusDI=30},FuturesAtrSignal=hot.FuturesAtrSignal! with{AtrRatio=1.6},FuturesMacdSignal=hot.FuturesMacdSignal! with{Histogram=-3}},hot with{UpdatedAtUtc=now.AddMinutes(-6),LatestItiTrendSignal=null,FuturesTdiSignal=null,FuturesRsiSignal=hot.FuturesRsiSignal! with{RSI=30},MdiPercent=10,MdiClassification=FuturesMDITrendType.DownTrending}};
        var entries=new List<object>();var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        for(int i=0;i<samples.Length;i++)foreach(var live in new[]{false,true}){
            var name=$"outlook-{i}-{live}";
            FixtureFiles.WriteAllBytes(Path.Combine(directory,name+".msgpack"),MessagePackSerializer.Serialize(samples[i],MessagePackBinarySerializer.Options));
            entries.Add(new{name,live,now,expected=MarketOutlookPresentation.Create(samples[i],live,new DateTimeOffset(now))});
        }
        FixtureFiles.WriteAllText(Path.Combine(directory,"outlook-display.json"),JsonSerializer.Serialize(entries,options));
        var values=new[]{"1.125","-1.125","0.005","-0.005","79228162514264337593543950335","0.0000000000000000000000000001"};
        FixtureFiles.WriteAllText(Path.Combine(directory,"decimal-display.json"),JsonSerializer.Serialize(values.Select(text=>new{text,fixed2=decimal.Parse(text,CultureInfo.InvariantCulture).ToString("F2",CultureInfo.InvariantCulture),percent2=decimal.Parse(text,CultureInfo.InvariantCulture).ToString("P2",CultureInfo.GetCultureInfo("en-CA"))}),options));
    }
}
