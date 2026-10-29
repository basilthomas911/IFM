using System.Text.Json;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
var date=new DateOnly(2026,10,26);var store=new DatabentoLastPriceStore(date,2);store.RegisterContract("O",AssetTypeId.FuturesOption);
var reader=store.GetFuturesOptionReader("O",date);var cases=new List<object>();var random=new Random(42);
void Apply(string operation,long sequence,long ticks,int risk) {
    var trade=new LastTradeTickSnapshot("O",date,risk/1000000000m,7,sequence,DateTimeOffset.UnixEpoch.AddTicks(ticks),DateTimeOffset.UnixEpoch.AddTicks(ticks+1));
    var quote=new LastQuoteTickSnapshot("O",date,risk/1000000000m,2,3,(risk+1)/1000000000m,4,5,sequence,trade.EventTimestamp,trade.ReceiveTimestamp){LocalReceivedAtUtc=trade.ReceiveTimestamp.AddTicks(1)};
    var greeks=new OptionGreeksSnapshot{IsValid=true,SolverIterations=risk};
    var changed=operation switch {"trade"=>store.TryUpdateTrade(trade),"quote"=>store.TryUpdateQuote(quote),"tradeRisk"=>store.TryUpdateTradeWithGreeks(new(trade,greeks)),"quoteRisk"=>store.TryUpdateQuoteWithGreeks(new(quote,greeks)),_=>false};
    if(operation=="reset")store.ResetContracts(["O"]);
    if(operation=="retire")store.Invalidate();
    var hasTrade=reader.TryGetLastTrade(out var t);var hasQuote=reader.TryGetLastQuote(out var q);var hasTradeRisk=reader.TryGetLastTradeWithGreeks(out var tr);var hasQuoteRisk=reader.TryGetLastQuoteWithGreeks(out var qr);
    cases.Add(new{operation,sequence,ticks,risk,changed,hasTrade,hasQuote,hasTradeRisk,hasQuoteRisk,tradeSequence=t.SourceSequence,tradeTicks=t.EventTimestamp==default?0:(t.EventTimestamp-DateTimeOffset.UnixEpoch).Ticks,tradePrice=(long)(t.Price*1000000000m),quoteSequence=q.SourceSequence,quoteTicks=q.EventTimestamp==default?0:(q.EventTimestamp-DateTimeOffset.UnixEpoch).Ticks,quotePrice=(long)((q.BidPrice??0)*1000000000m),tradeRisk=tr.Greeks.SolverIterations,quoteRisk=qr.Greeks.SolverIterations});
}
Apply("trade",1,100,1);Apply("tradeRisk",1,100,2);Apply("trade",1,101,3);Apply("tradeRisk",1,100,4);Apply("tradeRisk",1,101,5);Apply("trade",2,10,6);
Apply("quoteRisk",1,100,7);Apply("quote",1,101,8);Apply("quoteRisk",0,200,9);Apply("quoteRisk",1,101,10);
for(var i=0;i<2500;i++) {var operation=new[]{"trade","quote","tradeRisk","quoteRisk","reset"}[random.Next(5)];Apply(operation,random.Next(-1,30),random.Next(0,300),i+20);}
Apply("retire",1,1,1);Apply("trade",100,100,1);Apply("quoteRisk",100,100,1);
File.WriteAllText(args[0],JsonSerializer.Serialize(cases));Console.WriteLine($"Unchanged C# latest-price store: {cases.Count} transitions.");
