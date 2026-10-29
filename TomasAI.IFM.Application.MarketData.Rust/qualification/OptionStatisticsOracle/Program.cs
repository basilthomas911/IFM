// Invokes the unchanged current store; this executable never connects to a
// provider or database. Reflection accesses internal fixture-owned mutations.
using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Definitions;
var store=new OptionChainStateStore();var date=new DateOnly(2026,10,26);const string contract="ES20261218C6000";
OptionChainSessionKey[] keys=[new("ES20261218",new(2026,12,18),"","a"),new("ES20261218",new(2026,12,18),"","b")];
var route=new DatabentoOptionChainRoute{FuturesOptionContractId=contract,Definition=new(){Dataset="GLBX.MDP3",RawSymbol="fixture",Ticker="ES",
    Underlying="ES20261218",Instrument=new(3,43),Right=OptionRightSelection.Call,StrikePrice=6000,MaturityDate=new(2026,12,18)}};
object? Invoke(string name,params object?[] args)=>typeof(OptionChainStateStore).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(store,args);
foreach(var key in keys)Invoke("Create",key,new[]{route});
var sessions=(Dictionary<OptionChainSessionKey,Dictionary<string,OptionChainContractState>>)typeof(OptionChainStateStore)
    .GetField("_sessions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(store)!;
int DateNumber(DateOnly value)=>value.Year*10000+value.Month*100+value.Day;
long? Ticks(DateTimeOffset? value)=>value?.UtcTicks-DateTimeOffset.UnixEpoch.Ticks;
object Snapshot(int target){store.TryGet(keys[target],contract,out var s);return new{
    volume=s.SessionVolume,open_interest=s.OpenInterest,statistics_at=Ticks(s.StatisticsAtUtc),official=s.SessionVolumeOfficial,
    volume_date=s.VolumeValueDate is{}vd?DateNumber(vd):(int?)null,open_interest_date=s.OpenInterestValueDate is{}od?DateNumber(od):(int?)null,
    volume_at=Ticks(s.VolumeObservedAtUtc),open_interest_at=Ticks(s.OpenInterestObservedAtUtc),observed=s.ObservedSessionVolume,
    last_event_ns=s.LastVolumeEventNanoseconds,last_sequence=s.LastVolumeSequence,replay_complete=s.VolumeReplayComplete};}
var cases=new List<Dictionary<string,object?>>();
void Add(string op,int target=0,uint size=0,long eventNs=0,uint sequence=0,DateOnly? valueDate=null,
    long? volume=null,long? interest=null,DateOnly? reference=null,long at=0,int source=0,long observed=0){
    var day=valueDate??date;var referenceDate=reference??date;string? failure=null;
    try{switch(op){
        case "volume":Invoke("AccumulateVolume",keys[target],contract,size,eventNs,sequence,day);break;
        case "complete":Invoke("CompleteVolumeReplay",keys[target],contract,day);break;
        case "seed":Invoke("Seed",keys[target],new[]{new OptionChainStatisticsSnapshot(contract,Volume:volume,VolumeValueDate:referenceDate,
            OpenInterest:interest,OpenInterestValueDate:referenceDate)},day);break;
        case "stats":Invoke("UpdateStatistics",keys[target],contract,volume,interest,referenceDate,DateTimeOffset.UnixEpoch.AddTicks(at));break;
        case "copy":Invoke("CopyStatistics",keys[source],keys[target],contract);break;
        case "restore_observed":sessions[keys[target]][contract]=sessions[keys[target]][contract]with{
            ObservedSessionVolume=observed,LastVolumeEventNanoseconds=eventNs,LastVolumeSequence=sequence};break;
        case "reset":sessions[keys[target]][contract]=new(route,null,null);break;
        default:throw new InvalidOperationException(op);
    }}catch(TargetInvocationException error)when(error.InnerException is OverflowException){failure=nameof(OverflowException);}
    cases.Add(new(){["op"]=op,["target"]=target,["source"]=source,["size"]=size,["event_ns"]=eventNs,["sequence"]=sequence,
        ["date"]=DateNumber(day),["reference"]=DateNumber(referenceDate),["volume"]=volume,["interest"]=interest,["at"]=at,
        ["observed"]=observed,["failure"]=failure,["expected"]=Snapshot(target)});
}
Add("seed",volume:1000,interest:500);Add("volume",size:1,eventNs:100,sequence:1);Add("volume",size:999,eventNs:100,sequence:1);
Add("volume",size:999,eventNs:99,sequence:uint.MaxValue);Add("complete");Add("stats",volume:8000,interest:600,at:100);
Add("volume",size:5,eventNs:200,sequence:2);Add("copy",target:1,source:0);Add("stats",volume:123,reference:date.AddDays(-1),at:200);
Add("stats",volume:8010,at:50);Add("stats",volume:8020,at:100);Add("stats",volume:-1,interest:-1,at:300);
Add("seed",volume:99999,interest:99999,reference:date.AddDays(1));
Add("restore_observed",observed:long.MaxValue-1,eventNs:1000);Add("volume",size:2,eventNs:1001,sequence:1);
Add("volume",size:1,eventNs:1001,sequence:1);Add("volume",size:0,eventNs:1002,sequence:1);Add("volume",size:1,eventNs:1003,sequence:1);
Add("reset");Add("reset",target:1);
var random=new Random(2307);for(var i=0;i<1000;i++){
    var target=random.Next(2);var day=date.AddDays(random.Next(-1,2));var reference=date.AddDays(random.Next(-3,3));
    switch(random.Next(5)){
        case 0:Add("volume",target,(uint)random.Next(0,200),random.Next(-100,3000),(uint)random.Next(0,20),day);break;
        case 1:Add("complete",target,valueDate:day);break;
        case 2:Add("seed",target,volume:random.Next(-2,30000),interest:random.Next(-2,30000),reference:reference,valueDate:day);break;
        case 3:Add("stats",target,volume:random.Next(3)==0?null:random.Next(-2,30000),interest:random.Next(3)==0?null:random.Next(-2,30000),reference:reference,at:random.Next(-100,3000));break;
        default:Add("copy",target,source:random.Next(2));break;
    }
}
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Recorded {cases.Count} unchanged C# option statistics transitions.");
