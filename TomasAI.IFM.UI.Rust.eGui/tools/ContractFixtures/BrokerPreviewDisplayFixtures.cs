using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
internal static class BrokerPreviewDisplayFixtures {
 public static void Export(string output){
  var cases=new List<object>();
  void Add(string name,BrokerTradeLegData[] legs,TradeOrderPreview preview){
   var actual=BrokerTradePreviewPresentation.FromBackend(legs,preview,null);
   FixtureFiles.WriteAllBytes(Path.Combine(output,$"broker-preview-{name}.msgpack"),MessagePackSerializer.Serialize(preview,MessagePackBinarySerializer.ContentOptions));
   cases.Add(new{name,legs,expected=TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Map(actual,camelCase:true)});
  }
  Add("empty",[],new());
  var legs=new[]{new BrokerTradeLegData("ES-C","-SC",6050,1,2,.16,true,QuoteAtUtc:DateTimeOffset.Parse("2026-10-26T12:30:00Z"),Frozen:true)};
  var preview=new TradeOrderPreview{HasCompleteQuotes=true,HasBalancedQuantities=true,OrderPrice=-.1234567890123456789012345678m,NetBid=-1234.565m,NetAsk=-1234.555m,NetMid=-1234.560m,NetDelta=1.23445,NetVega=-.00005,CallWidth=50,CallSpreadValue=1.005m,HasPayoffEstimate=true,MaximumProfit=12.005m,MaximumLoss=1234.565m,EstimatedCredit=100.015m,MaximumReturn=.123456789m};
  Add("priced",legs,preview);
  Add("unlimited",legs,preview with{HasUnlimitedLoss=true,HasUnlimitedProfit=true,MaximumLoss=null,MaximumProfit=null,MaximumReturn=null});
  Add("unequal",legs,preview with{HasBalancedQuantities=false});
  Add("future",[new("ESZ6","+Future",null,null,null,null,false,Multiplier:50,IsFuture:true)],new(){OrderPrice=6050.25m,NetMid=6050.25m,NetDelta=150,NetVega=0});
  FixtureFiles.WriteAllText(Path.Combine(output,"broker-preview-display.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
 }
}
