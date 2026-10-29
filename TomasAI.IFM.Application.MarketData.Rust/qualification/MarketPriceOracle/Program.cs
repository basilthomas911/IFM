using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
var type=Assembly.Load("TomasAI.IFM.Framework.MarketData.DataBento").GetType("TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.TickAggregationService+MarketPriceCache",true)!;
var date=new DateOnly(2026,10,26);var cases=new List<object>();var random=new Random(9393);
foreach(long? tick in new long?[]{null,-1,0,1,250000000}){
 var mapping=new TickContractMapping("GLBX.MDP3",date,1,42,"ES20261218",AssetTypeId.Futures,new TickerContractDetails{ContractId="ES20261218",Dataset="GLBX.MDP3",DefinitionDate=date,PublisherId=1,InstrumentId=42,AssetTypeId=AssetTypeId.Futures,TickSize=tick/1000000000m});
 var cache=Activator.CreateInstance(type,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[mapping],null)!;
 for(int i=0;i<1000;i++){
  var quote=random.Next(0,3)==0;var sequence=random.Next(0,100);long timestamp=random.Next(0,10000);long price=6500000000000+random.Next(-20,20)*10000000L;var selectedDate=date.AddDays(i%100==0?1:0);
  var when=DateTimeOffset.UnixEpoch.AddTicks(timestamp);object?[] inputs=quote?[selectedDate,new FuturesMarketQuoteSnapshot(price/1000000000m,5,(price+250000000)/1000000000m,6,1,2,sequence,when,when),null]:[selectedDate,new FuturesMarketTradeSnapshot(price/1000000000m,7,sequence,when,when),null,false];
  var accepted=(bool)type.GetMethod(quote?"TryUpdateQuote":"TryUpdateTrade")!.Invoke(cache,inputs)!;var snapshot=(FuturesMarketPriceSnapshot)inputs[2]!;
  cases.Add(new{tick,reset=i==0,quote,sequence,timestamp,price,date=selectedDate.ToString("yyyy-MM-dd"),accepted,changed=quote?accepted:(bool)inputs[3]!,snapshotDate=snapshot.ValueDate.ToString("yyyy-MM-dd"),quoteSequence=snapshot.Quote?.SourceSequence,quoteTime=snapshot.Quote?.EventTimestamp.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks,quotePrice=snapshot.Quote?.BidPrice is {} bid?(long?)(bid*1000000000m):null,tradeSequence=snapshot.Trade?.SourceSequence,tradeTime=snapshot.Trade?.EventTimestamp.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks,tradePrice=snapshot.Trade?.LastPrice is {} last?(long?)(last*1000000000m):null});
 }
}
File.WriteAllText(args[0],JsonSerializer.Serialize(cases));Console.WriteLine($"C# market-price cache: {cases.Count} transitions");
