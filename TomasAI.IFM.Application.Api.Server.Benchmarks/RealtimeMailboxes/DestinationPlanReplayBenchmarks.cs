using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Same actual actor path and Function/exit substitutes; after additionally owns request/output on disk before dispatch.</summary>
[MemoryDiagnoser]
public class DestinationPlanReplayBenchmarks
{
    const int Batch=8;
    [Params(false,true)] public bool RequiresExit {get;set;}
    VerticalSpreadPositionDeliveryFixture before=null!,after=null!;
    string directory="";
    [IterationSetup]
    public void Prepare()
    {
        directory=Path.Combine("E:/IFM/BenchmarkArtifacts/destination-v2-cases",Guid.NewGuid().ToString("N"));
        before=new(false,context=>new LegacyVerticalSpreadPositionRealtimeActor(context)){RequiresExit=RequiresExit};
        after=new(false,positionIntentDirectory:directory){RequiresExit=RequiresExit};
        before.Start().GetAwaiter().GetResult();after.Start().GetAwaiter().GetResult();
    }
    [Benchmark(Baseline=true,OperationsPerInvoke=Batch)]
    public async Task BeforeUnfrozenRequest()
    {for(var i=1;i<=Batch;i++)if(!await before.Deliver(VerticalSpreadPositionDeliveryFixture.Input(i)))throw new InvalidDataException("Baseline failed.");}
    [Benchmark(OperationsPerInvoke=Batch)]
    public async Task AfterDurableRequestAndOutput()
    {for(var i=1;i<=Batch;i++)if(!await after.Deliver(VerticalSpreadPositionDeliveryFixture.Input(i)))throw new InvalidDataException("Durable replay failed.");}
    [IterationCleanup]
    public void Cleanup()
    {
        before.DisposeAsync().AsTask().GetAwaiter().GetResult();after.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
}
