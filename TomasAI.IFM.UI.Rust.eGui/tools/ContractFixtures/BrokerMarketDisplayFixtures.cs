using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Rust.Bridge.Net;
internal static class BrokerMarketDisplayFixtures {
 public static void Export(string output){
  var options=MessagePackBinarySerializer.ContentOptions;
  var at=new DateTimeOffset(2026,10,26,8,30,1,TimeSpan.FromHours(-4)).AddTicks(1234567);
  var original=MessagePackSerializer.Deserialize<EvaluatedOptionChainReadModel>(File.ReadAllBytes(Path.Combine(output,"option-display-merged.msgpack")),options);
  var market=original with{AsOfUtc=at,Contracts=original.Contracts.Select((c,i)=>i==0?c with{QuoteAtUtc=at,Volume=123456789,VolumeValueDate=new(2026,10,26)}:c).ToArray()};
  FixtureFiles.WriteAllBytes(Path.Combine(output,"broker-market-input.msgpack"),MessagePackSerializer.Serialize(market,options));
  var leg=new BrokerTradeLegData("ES-C","-SC",6050.0000000000000000000000001m,1.2500m,1.5m,.16,true,Vega:.3,Multiplier:50,QuoteAtUtc:at,Frozen:true);
  FixtureFiles.WriteAllText(Path.Combine(output,"broker-market-leg-input.json"),JsonSerializer.Serialize(leg));
  void Write(string name,string typeName,object value){
   var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net."+typeName)!;
   var display=type.GetMethod("From",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public)!.Invoke(null,[value])!;
   FixtureFiles.WriteAllBytes(Path.Combine(output,name+".msgpack"),MessagePackSerializer.Serialize(type,display,options));
  }
  Write("broker-market-display","OptionChainDisplay",market);
  Write("broker-market-leg-display","SelectedLegDisplay",leg);
 }
}
