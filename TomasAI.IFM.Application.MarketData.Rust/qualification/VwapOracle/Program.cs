using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
var type=Assembly.Load("TomasAI.IFM.Framework.MarketData.DataBento").GetType("TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.FuturesVwapSourceAccumulator",true)!;
var state=Activator.CreateInstance(type,nonPublic:true)!;var cases=new List<object>();
Guid G(int id)=>new(id,0,0,new byte[8]);int Id(Guid id)=>BitConverter.ToInt32(id.ToByteArray());
uint[] Bits(decimal d)=>decimal.GetBits(d).Select(v=>unchecked((uint)v)).ToArray();
void Apply(string operation,long price=0,long size=0,long sequence=0,long timestamp=0,int action=1,int conditions=0,int epoch=1,long ordinal=1){
 bool failed=false;try{
 if(operation=="reset")type.GetMethod("Reset")!.Invoke(state,[G(epoch)]);
 else if(operation=="complete")type.GetMethod("CompleteReplay")!.Invoke(state,[G(epoch)]);
 else if(operation=="replay")type.GetMethod("ApplyReplay")!.Invoke(state,[price/1000000000m,size,sequence,DateTimeOffset.UnixEpoch.AddTicks(timestamp),(NormalizedTradeAction)action,(NormalizedTradeConditionFlags)conditions]);
 else type.GetMethod("ApplyLive")!.Invoke(state,[price/1000000000m,size,sequence,DateTimeOffset.UnixEpoch.AddTicks(timestamp),(NormalizedTradeAction)action,(NormalizedTradeConditionFlags)conditions,G(epoch),ordinal]);
 }catch(TargetInvocationException e)when(e.InnerException is OverflowException){failed=true;}
 var s=(FuturesVwapSourceCheckpoint)type.GetMethod("Snapshot")!.Invoke(state,null)!;
 cases.Add(new{operation,price,size,sequence,timestamp,action,conditions,epoch,ordinal,failed,stream=Id(s.StreamEpochId),lastOrdinal=s.LastTradeOrdinal,lastSequence=s.LastTradeSourceSequence,at=s.AsOfUtc.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks,pv=Bits(s.CumulativePriceVolume),volume=s.CumulativeVolume,eligible=s.EligibleTradeCount,rejected=s.RejectedTradeCount,last=Bits(s.LastPrice),valid=s.IsValid,complete=s.IsReplayComplete,generation=Id(s.RecoveryGenerationId),reason=(byte)s.InvalidReason});
}
Apply("reset");Apply("replay",6500250000000,4,1);Apply("complete");Apply("live",6500250000001,2,2,ordinal:1);Apply("live",6500250000000,1,3,ordinal:3);Apply("reset",epoch:2);Apply("live",1,1,4,epoch:2);Apply("reset");
var rng=new Random(6161);for(int i=0;i<2000;i++){
 if(i%47==0)Apply("reset",epoch:i%3+1);if(i%47==1)Apply("complete",epoch:i%3+1);
 Apply(i%3==0?"replay":"live",rng.NextInt64(-1,long.MaxValue),rng.Next(0,10)==0?long.MaxValue:rng.NextInt64(-1,1000000000),i,i*100,action:rng.Next(0,7),conditions:rng.Next(0,5)==0?1<<8:rng.Next(0,7),epoch:i%3+1,ordinal:i%47);
}
// Repeated huge eligible inputs force 96-bit scale reduction, half-even rounding,
// decimal overflow and then signed-volume overflow in the original mutation order.
for(int i=0;i<80;i++) {Apply("reset");Apply("complete");long price=long.MaxValue-i;for(int n=0;n<6;n++)Apply("live",price,long.MaxValue/3,n+1,ordinal:n+1);}
File.WriteAllText(args[0],JsonSerializer.Serialize(cases));Console.WriteLine($"C# VWAP accumulator: {cases.Count} transitions including decimal/quantity overflow");
