using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
[MemoryDiagnoser]
public class EmulatorCheckpointBenchmarks
{
    const int Batch=32;
    [Params(1,4)]public int Legs{get;set;}
    [Params("Fill","Replay","NoMatch")]public string Mode{get;set;}="Fill";
    sealed class Clock:IEmulatorClock{public DateTime UtcNow=>new(2026,11,20,15,0,0,DateTimeKind.Utc);}
    sealed record Case(LegacyCheckpointEmulatorLedger Before,EmulatorLedger After,FileEmulatorLedgerStore Store,EmulatorQuote Target);
    Case[] cases=[];
    [IterationSetup]public void Prepare()
    {
        Cleanup();
        var root=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/emulator-checkpoint-bdn-cases",Guid.NewGuid().ToString("N"));
        cases=Enumerable.Range(0,Batch).Select(index=>
        {
            var clock=new Clock();var scenario=new EmulatorScenario("BENCH","USD",1_000_000m,.65m,TimeSpan.FromMinutes(1));
            var oldStore=new LegacyCheckpointFileStore(Path.Combine(root,index.ToString(System.Globalization.CultureInfo.InvariantCulture),"before.json"));
            var store=new FileEmulatorLedgerStore(Path.Combine(root,index.ToString(System.Globalization.CultureInfo.InvariantCulture),"after.json"));
            var before=new LegacyCheckpointEmulatorLedger(scenario,clock,oldStore);var after=new EmulatorLedger(scenario,clock,store);
            var limit=Mode=="NoMatch"?0m:20m;
            var request=new FrameworkOrderRequest("BENCH","order",Guid.NewGuid(),Guid.NewGuid(),Legs==1?FrameworkOrderShape.FuturesOutright:FrameworkOrderShape.IronCondor,false,
                Enumerable.Range(0,Legs).Select(i=>new FrameworkOrderLeg(Guid.NewGuid(),"C"+i,i%2==0?1:-1,Legs==1?null:100m+i,null,null,50m)).ToArray(),
                limit,limit,limit,.25m,clock.UtcNow.AddMinutes(5),"approved","reference",1000m,1000m);
            before.Place(request);after.Place(request);
            var quotes=Enumerable.Range(0,Legs).Select(i=>new EmulatorQuote("C"+i,9m,10m,10,10,clock.UtcNow,1,i+1)).ToArray();
            foreach(var quote in quotes[..^1]){before.PublishQuote(quote);after.PublishQuote(quote);}
            if(Mode=="Replay"){before.PublishQuote(quotes[^1]);after.PublishQuote(quotes[^1]);}
            return new Case(before,after,store,quotes[^1]);
        }).ToArray();
    }
    [Benchmark(Baseline=true,OperationsPerInvoke=Batch)]public int BeforeCheckpoint()=>cases.Sum(item=>item.Before.PublishQuote(item.Target));
    [Benchmark(OperationsPerInvoke=Batch)]public int AfterRecoverableCommit()=>cases.Sum(item=>item.After.PublishQuote(item.Target));
    [IterationCleanup]public void Cleanup(){foreach(var item in cases)item.Store.Dispose();cases=[];}
}
