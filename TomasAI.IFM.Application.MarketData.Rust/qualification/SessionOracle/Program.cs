using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
var type=Assembly.Load("TomasAI.IFM.Framework.MarketData.DataBento").GetType("TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.FuturesSessionAccumulator",true)!;
var accumulator=Activator.CreateInstance(type,nonPublic:true)!;
var date=new DateOnly(2026,10,26);
var cases=new List<object>();
void Apply(string operation,uint sequence=0,uint size=0,bool replay=false,ushort kind=0,long price=0,long quantity=0,long reference=0,byte action=1,byte flags=0) {
    object?[] inputs;
    if(operation=="trade") inputs=["ESZ6",date,new TradeRecord64(new MarketRecordHeader32(42,7,MarketRecordKind.Trade,flags,sequence*1000,0,sequence),price,size,(byte)'T',(byte)'N',0),replay,null];
    else if(operation=="statistic") inputs=["ESZ6",date,new StatisticsRecord64(new MarketRecordHeader32(42,7,MarketRecordKind.Statistics,flags,sequence*1000,0,sequence),price,quantity,reference,kind,0,action,0),null];
    else inputs=["ESZ6",date];
    var method=type.GetMethod(operation=="trade"?"TryAccumulateTrade":operation=="statistic"?"TryApplyStatistic":"CompleteTradeReplay")!;
    var result=method.Invoke(accumulator,inputs);
    var snapshot=operation=="complete"?(FuturesSessionStatisticsSnapshot)result!:(FuturesSessionStatisticsSnapshot)inputs[^1]!;
    cases.Add(new {operation,sequence,size,replay,kind,price,quantity,reference,action,flags,changed=operation=="complete" || (bool)result!,date=snapshot.ValueDate.ToString("yyyy-MM-dd"),open=decimal.ToInt64(snapshot.OpenPrice*1000000000m),high=decimal.ToInt64(snapshot.HighPrice*1000000000m),low=decimal.ToInt64(snapshot.LowPrice*1000000000m),source=snapshot.SourceSequence,eventNs=snapshot.EventTimestampNanoseconds,volume=snapshot.Volume,quality=(int)snapshot.VolumeQuality,prices=snapshot.HasPriceStatistics,hasVolume=snapshot.HasVolume,any=snapshot.HasAnyData});
}
Apply("trade",0,4,true);Apply("trade",0,4,true);Apply("trade",1,uint.MaxValue,true);Apply("complete");Apply("trade",1,9);Apply("trade",2,25);
Apply("statistic",1,kind:1,price:6500000000000);Apply("statistic",2,kind:4,price:6400000000000);Apply("statistic",3,kind:5,price:6600000000000);
Apply("statistic",4,kind:1,price:6500000000000);Apply("statistic",3,kind:1,price:6510000000000);Apply("statistic",0,kind:4,price:6300000000000);
Apply("statistic",5,kind:6,quantity:long.MaxValue);Apply("statistic",6,kind:6,quantity:-1);Apply("statistic",7,kind:6,quantity:9000);Apply("trade",8,100);Apply("complete");
long reference=new DateTimeOffset(2026,10,23,0,0,0,TimeSpan.Zero).ToUnixTimeMilliseconds()*1000000;
Apply("statistic",10,kind:6,quantity:8000,reference:reference);Apply("statistic",11,kind:1,price:1,action:2);Apply("statistic",12,kind:1,price:1,flags:4);
var rng=new Random(12345);
for(uint i=20;i<1020;i++) {var kind=new ushort[]{1,4,5,6,99}[rng.Next(5)];Apply("statistic",i,kind:kind,price:rng.Next(0,3)==0?-1:rng.Next(6300,6700)*1000000000L,quantity:rng.Next(-1,10000),reference:rng.Next(0,4)==0?reference:0,action:(byte)rng.Next(0,3),flags:(byte)rng.Next(0,8));}
File.WriteAllText(args[0],JsonSerializer.Serialize(cases));Console.WriteLine($"Unchanged C# session accumulator: {cases.Count} transitions.");
