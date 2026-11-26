using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Same original ITI commands and legacy WAL/receipt formats; only durable group size changes.</summary>
[MemoryDiagnoser]
public class ItiOrderedSourceBatchBenchmarks
{
    [Params(1,64)]public int BatchSize{get;set;}
    FuturesItiInputWal before=null!,after=null!;GenerateFuturesItiSignalCommand[] commands=[];byte[][] payloads=[];
    string root="";
    [IterationSetup]
    public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"ifm-iti-group-bdn-"+Guid.NewGuid().ToString("N"));
        before=new(Path.Combine(root,"before"));after=new(Path.Combine(root,"after"));
        var at=new DateTime(2026,11,20,15,0,0,DateTimeKind.Utc);var epoch=Guid.NewGuid();
        commands=Enumerable.Range(1,BatchSize).Select(index=>new GenerateFuturesItiSignalCommand("ES20261218",new(2026,11,20),TimeFrameType.Daily,at.AddMilliseconds(index),6510+index*.25,20)
        {CommandId=Guid.NewGuid(),InputsFrozen=true,SourceCaptureRequired=true,SourceEventId=Guid.NewGuid(),
            SourceCursor=new("GLBX.MDP3",1,42,epoch,index,index,at.AddMilliseconds(index),at.AddMilliseconds(index+1))}).ToArray();
        payloads=commands.Select(FuturesItiJournalEnvelope.Encode).ToArray();
        for(var index=0;index<commands.Length;index++)commands[index]=commands[index] with{JournalPayload=payloads[index]};
    }
    [Benchmark(Baseline=true)]
    public async Task BeforeSerialDurablePreparationAndRelease()
    {
        for(var index=0;index<commands.Length;index++)await before.AppendAsync(payloads[index],default);
        foreach(var command in commands)before.ReleaseSource(command);
    }
    [Benchmark]
    public async Task AfterGroupedDurablePreparationAndRelease()
    {
        await after.AppendBatchAsync(payloads,default);after.ReleaseSourcesBatch(commands);
    }
    [IterationCleanup]
    public void Cleanup(){before.DisposeAsync().AsTask().GetAwaiter().GetResult();after.DisposeAsync().AsTask().GetAwaiter().GetResult();Directory.Delete(root,true);}
}
