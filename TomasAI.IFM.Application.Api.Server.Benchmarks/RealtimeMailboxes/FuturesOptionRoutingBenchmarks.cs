using BenchmarkDotNet.Attributes;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
[MemoryDiagnoser]
public class FuturesOptionRoutingBenchmarks
{
    [Params("Fresh","Replay")] public string Mode {get;set;}="Fresh";
    [Params("Quote","Pricing")] public string Kind {get;set;}="Quote";
    FuturesOptionRoutingDeliveryFixture before=null!,after=null!;int beforeSequence=1,afterSequence=1;
    [GlobalSetup] public async Task Setup()
    {
        before=new(factory:context=>new LegacyFuturesOptionRoutingActor(context));
        after=new(Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/option-routing-bdn-captures",Guid.NewGuid().ToString("N")));
        await before.Start();await after.Start();
        if(!await before.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1,Kind))||!await after.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1,Kind)))throw new InvalidDataException("Setup failed.");
    }
    [IterationCleanup] public void ResetMeasurements(){before.ResetMeasurements();after.ResetMeasurements();}
    [Benchmark(Baseline=true)] public Task<bool> Before()=>Deliver(before,Mode=="Fresh"?++beforeSequence:1,Kind);
    [Benchmark] public Task<bool> AfterDurableFanout()=>Deliver(after,Mode=="Fresh"?++afterSequence:1,Kind);
    static async Task<bool> Deliver(FuturesOptionRoutingDeliveryFixture fixture,int sequence,string kind)
    {if(!await fixture.Deliver(FuturesOptionRoutingDeliveryFixture.Input(sequence,kind)))throw new InvalidDataException("Measured input failed.");return true;}
    [GlobalCleanup]public async Task Cleanup(){await before.DisposeAsync();await after.DisposeAsync();}
}
