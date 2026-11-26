using BenchmarkDotNet.Attributes;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
[MemoryDiagnoser]
public class BrokerOrderSettlementBenchmarks
{
    [Params("Futures","Option")]public string Kind{get;set;}="Futures";
    [Params(false,true)]public bool Failed{get;set;}
    BrokerOrderDeliveryFixture before=null!,after=null!;
    [GlobalSetup]public async Task Setup()
    {
        before=new(context=>new LegacyBrokerOrderRealtimeActor(context)){Failure=Failed?"throw":"none"};after=new(){Failure=Failed?"throw":"none"};
        await before.Start();await after.Start();
        if(!await before.Deliver(BrokerOrderDeliveryFixture.Input(Kind,1))||await after.Deliver(BrokerOrderDeliveryFixture.Input(Kind,1))!=!Failed)throw new InvalidDataException("Expected settlement boundary not reproduced.");
    }
    [IterationCleanup]public void Reset(){before.Reset();after.Reset();}
    [Benchmark(Baseline=true)]public Task<bool> BeforeMetricsDependent()=>before.Deliver(BrokerOrderDeliveryFixture.Input(Kind,1));
    [Benchmark]public Task<bool> AfterRequiredSettlement()=>after.Deliver(BrokerOrderDeliveryFixture.Input(Kind,1));
    [GlobalCleanup]public async Task Cleanup(){await before.DisposeAsync();await after.DisposeAsync();}
}
