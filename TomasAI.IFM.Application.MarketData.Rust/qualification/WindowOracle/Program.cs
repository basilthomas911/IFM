using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Host.OptionChains;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
// Immutable oracle from the unchanged managed selection helpers. No provider,
// credentials, native DLL, live session or order endpoint is opened.
var cases=new List<object>();var random=new Random(9137);
for(var n=0;n<1200;n++) {
 var count=n%11==0?0:random.Next(1,300);var values=new List<FuturesOptionContractReadModel>();
 for(var i=0;i<count;i++) {
  var strike=n%7==0?1.125+i*0.125:5500+random.Next(0,200)*5d;
  values.Add(new(){ContractId=$"ES-{i}",StrikePrice=strike,OptionType=i%2==0?"Call":"Put"});
  if(i%19==0)values.Add(values[^1] with{StrikePrice=strike+100}); // first duplicate wins
 }
 decimal? price=n%13==0?null:n%7==0?10.125m:6000m+(decimal)(random.NextDouble()*10);
 decimal? deviation=n%3==0?null:15.125m+(n%31);
 var multiplier=new[]{1.0,1.65,2.5,3.125,4.0}[n%5];var iv=0.1+(n%23)*0.01;
 var required=values.Where((_,i)=>i%37==0).Select(x=>x.ContractId).ToArray();
 var at=new DateTimeOffset(2026,10,27,14,0,0,TimeSpan.Zero);var seconds=1+n*101;
 var mode=n%2==0&&price>0?"iv":"band";
 var result=mode=="iv"?OptionChainStrikeWindow.SelectImpliedVolatility(values,price!.Value,iv,at.AddSeconds(seconds),at,requiredContractIds:required)
  :OptionChainStrikeWindow.Select(values,price,deviation,multiplier,required);
 var wing=n%4==0?-5m:n%9;
 var buffer=OptionChainSubscriptionCoverage.Buffer(values.ToArray(),result,wing);
 cases.Add(new{mode,price=price?.ToString(System.Globalization.CultureInfo.InvariantCulture),deviation=deviation?.ToString(System.Globalization.CultureInfo.InvariantCulture),multiplier,iv,seconds,wing=wing.ToString(System.Globalization.CultureInfo.InvariantCulture),required,
  rows=values.Select(x=>new{x.ContractId,x.StrikePrice,x.OptionType}),selected=result.Contracts.Select(x=>x.ContractId),
  lower=result.LowerBound is{} lower?decimal.GetBits(lower).Select(x=>unchecked((uint)x)).ToArray():null,
  upper=result.UpperBound is{} upper?decimal.GetBits(upper).Select(x=>unchecked((uint)x)).ToArray():null,
  result.Method,buffer=buffer.Select(x=>x.ContractId)});
}
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(cases));
Console.WriteLine($"PASS exported {cases.Count} managed strike-window and subscription-buffer oracle cases.");
