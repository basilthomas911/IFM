using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class BrokerObservationBenchmarks
{
    const int Batch = 8;
    [Params(1,4)] public int Legs { get; set; }
    BrokerObservationDeliveryFixture[] before = [], after = [];
    bool afterRan;

    [IterationSetup] public void Prepare()
    {
        Cleanup();
        var root=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/broker-observation-bdn-cases",Guid.NewGuid().ToString("N"));
        before=Enumerable.Range(0,Batch).Select(i=>new BrokerObservationDeliveryFixture(Path.Combine(root,"before",i.ToString(System.Globalization.CultureInfo.InvariantCulture),"ledger.json"),Legs)).ToArray();
        after=Enumerable.Range(0,Batch).Select(i=>new BrokerObservationDeliveryFixture(Path.Combine(root,"after",i.ToString(System.Globalization.CultureInfo.InvariantCulture),"ledger.json"),Legs)).ToArray();
    }
    [Benchmark(Baseline=true,OperationsPerInvoke=Batch)] public async Task BeforeUnsettledPublication()
    {
        foreach(var item in before)
        {
            await using var bridge=new LegacyBrokerObservationBridge(item.Real.Broker,item.Supervisor,NullLogger<BrokerOrderObservationBridge>.Instance);
            await bridge.StartAsync();await bridge.StopAsync();
        }
    }
    [Benchmark(OperationsPerInvoke=Batch)] public async Task AfterDurablePublicationCursor()
    {
        foreach(var item in after)
        {
            await using var bridge=item.Bridge();await bridge.StartAsync();
            await bridge.StopAsync();
        }
        afterRan=true;
    }
    [IterationCleanup] public void Cleanup()
    {
        if(afterRan)
            foreach(var item in after)
                if(item.Real.Checkpoints.Load()!.PublishedOrderObservationCount!=item.ExpectedCursor)
                    throw new InvalidDataException("Observation drain did not finish.");
        foreach(var item in before.Concat(after))item.DisposeAsync().GetAwaiter().GetResult();
        before=[];after=[];afterRan=false;
    }
}
