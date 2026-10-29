using System.Reflection;
using System.Text.Json;
using System.Globalization;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
var date=new DateOnly(2026,10,27);var output=new List<object>();
var assembly=typeof(NativeBrowserFacts).Assembly;
var contextType=assembly.GetType("TomasAI.IFM.Application.MarketData.Rust.Host.OptionChains.HostOptionChainContext",true)!;
var method=assembly.GetType("TomasAI.IFM.Application.MarketData.Rust.Host.OptionChains.OptionChainWindowInputs",true)!.GetMethod("ReadStoredAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
var values=new[]{0d,-1d,15.125d,double.NaN,double.PositiveInfinity,double.Epsilon,double.MaxValue};
for(var n=0;n<1200;n++) {
 FuturesEodDataV2ReadModel? current=n%5==0?null:new(){ContractId="ES20261218",ValueDate=date.AddDays(-n%3),ClosePrice=n%7==0?-1m:6001.125m,DailyStdDevAmount=values[n%values.Length]};
 FuturesEodDataV2ReadModel? previous=n%7==0?null:new(){ContractId="ES20261218",ValueDate=date.AddDays(-1),ClosePrice=n%11==0?-5m:5998m,DailyStdDevAmount=values[(n/7)%values.Length]};
 FuturesBbSignalReadModel? daily=n%3==0?null:new(){Price=n%13==0?-1m:6000m,StandardDeviation20=n%5==0?null:n%17==0?-5m:10.125m,Metadata=new(){ValueDate=n%19==0?date.AddDays(-1):date,IsValid=n%23!=0}};
 FuturesBbSignalReadModel? intraday=n%11==0?null:new(){Price=n%29==0?-1m:6002.25m,StandardDeviation20=n%7==0?null:n%31==0?0m:8.75m,Metadata=new(){ValueDate=n%37==0?date.AddDays(-1):date,IsValid=n%41!=0}};
 var db=Proxy.Create<IMarketDataDbContext>((m,a)=>m.Name switch{
  "GetFuturesEodDataAsync"=>Task.FromResult(current),"GetLastFuturesEodDataAsync"=>Task.FromResult(previous),
  "GetLatestFuturesBollingerBandSignalAsync"=>Task.FromResult(daily),"GetLatestFuturesBollingerBandSignalForTimeFrameAsync"=>Task.FromResult(intraday),_=>throw new Exception("Unexpected raw browser read: "+m.Name)});
 var factory=Proxy.Create<IDbContextFactory>((m,a)=>m.Name=="get_MarketDataDb"?db:throw new Exception("Unexpected database access."));
 var context=Activator.CreateInstance(contextType,new object?[]{factory,null,null,null,null,null,null,null,TimeProvider.System});
 if(context is null)throw new Exception("Could not create the baseline context.");
 var input=JsonSerializer.SerializeToElement(new{contract="ES20261218",root="ES",date=date.DayNumber});
 var raw=JsonDocument.Parse(await new NativeBrowserFacts(factory).ReadAsync(input,default)).RootElement.Clone();
 try {
  var result=await (Task<(decimal? Price,decimal? Deviation,DateOnly? Observation)>)method.Invoke(null,new object?[]{context,"ES","ES20261218",date,CancellationToken.None})!;
  output.Add(new{date=date.DayNumber,raw,failed=false,price=result.Price?.ToString(CultureInfo.InvariantCulture),deviation=result.Deviation?.ToString(CultureInfo.InvariantCulture),observed=result.Observation?.DayNumber});
 }catch(OverflowException){output.Add(new{date=date.DayNumber,raw,failed=true});}
}
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(output));
Console.WriteLine($"PASS exported {output.Count} actual managed fallback outcomes from the raw native storage projection.");
public class Proxy:DispatchProxy {
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 public static T Create<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Handler=handler;return proxy;}
 protected override object? Invoke(MethodInfo? method,object?[]? arguments)=>Handler(method!,arguments);
}
