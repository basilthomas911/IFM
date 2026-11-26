using BenchmarkDotNet.Attributes;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
[MemoryDiagnoser]
public class FuturesRoutingBenchmarks
{
    [Params("Fresh","Replay")] public string Mode {get;set;}="Fresh";
    FuturesRoutingDeliveryFixture before=null!,after=null!;int beforeSequence=1,afterSequence=1;
    [GlobalSetup] public async Task Setup()
    {
        before=new(factory:context=>new LegacyFuturesRoutingActor(context));
        after=new(Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/futures-routing-bdn-captures",Guid.NewGuid().ToString("N")));
        await before.Start();await after.Start();
        if(!await before.Deliver(FuturesRoutingDeliveryFixture.Input(1))||!await after.Deliver(FuturesRoutingDeliveryFixture.Input(1)))throw new InvalidDataException("Setup failed.");
    }
    [IterationCleanup] public void ResetMeasurements(){before.ResetMeasurements();after.ResetMeasurements();}
    [Benchmark(Baseline=true)] public Task<bool> Before()=>Deliver(before,Mode=="Fresh"?++beforeSequence:1);
    [Benchmark] public Task<bool> AfterDurableFanout()=>Deliver(after,Mode=="Fresh"?++afterSequence:1);
    static async Task<bool> Deliver(FuturesRoutingDeliveryFixture fixture,int sequence)
    {if(!await fixture.Deliver(FuturesRoutingDeliveryFixture.Input(sequence)))throw new InvalidDataException("Measured input failed.");return true;}
    [GlobalCleanup]public async Task Cleanup(){await before.DisposeAsync();await after.DisposeAsync();}
}
