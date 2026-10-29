using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
// Calls the unchanged current backend estimator; no provider/runtime/database I/O.
var at=new DateTimeOffset(2026,10,27,14,0,0,TimeSpan.Zero);var random=new Random(5189);var tests=new List<object>();
string Bits(double value)=>BitConverter.DoubleToUInt64Bits(value).ToString("X16",CultureInfo.InvariantCulture);
for(var test=0;test<1600;test++){
 var forward=test%29==0?0m:test%31==0?-1m:test%43==0?decimal.MaxValue:test%47==0?0.0000000000000000000000000001m:6000m+test%7*.125m;
 var input=new List<object>();var values=new List<CompositionInstrumentSnapshot>();var count=test%17==0?0:random.Next(1,100);
 for(var i=0;i<count;i++){
  var contract=(i%11==0?"\ue000":i%13==0?"\U0001f600":"contract")+"-"+random.Next(0,15);contract=contract.Replace("\\ue000","\ue000").Replace("\\U0001f600","\U0001f600");
  decimal? strike=i%23==0?null:i%19==0?-1m:6000m+(random.Next(-15,16)*2.5m);
  bool? call=i%9==0?null:i%2==0;var quoted=i%7!=0;
  double? full=i%4==0?null:i%37==0?double.NaN:i%31==0?double.PositiveInfinity:i%17==0?0:i%13==0?-.1:0.1+random.Next(1,40)*.01;
  double? selection=i%5==0?null:i%41==0?double.NegativeInfinity:0.1+random.Next(1,40)*.01;
  var calculated=at.AddTicks(i%6==0?1:-1);var until=at.AddTicks(i%8==0?0:10000000);
  var quote=new OptionPricingQuote(contract,1,2,3,4,at,at,1,Guid.Empty);
  var instrument=new CompositionMarketInstrument(contract,quoted?quote:null,null,strike,call,null){Selection=selection is{} iv?new(1,.5,iv,quote,quote,at,calculated,until,"fixture","fixture"):null};
  values.Add(new(instrument,full is{} risk?new(risk,0,0,0,0,0,1,.1,"fixture"):null));
  input.Add(new{id=contract,strike=strike?.ToString(CultureInfo.InvariantCulture),call,quoted,full=full is{} f?Bits(f):null,selection=selection is{} s?Bits(s):null,calculated=calculated.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks,until=until.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks});
 }
 var snapshot=new MarketCompositionSnapshot(1,Guid.Empty,"fixture","fixture","Daily",Guid.Empty,at,at.AddMinutes(1),values.ToImmutableArray(),"fixture");
 var actual=OptionChainAtmIvEstimator.Read(snapshot,forward);
 tests.Add(new{forward=forward.ToString(CultureInfo.InvariantCulture),at=at.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks,rows=input,expected=actual is{} result?Bits(result):null});
}
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(tests));Console.WriteLine($"PASS exported {tests.Count} unchanged C# ATM-IV oracle cases.");
