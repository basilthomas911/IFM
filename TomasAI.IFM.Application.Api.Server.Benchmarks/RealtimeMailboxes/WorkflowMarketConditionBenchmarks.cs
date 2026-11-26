using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class WorkflowMarketConditionBenchmarks
{
    const int Batch=16;
    [Params("New","CompletedReplay")]public string Mode{get;set;}="New";
    WorkflowDispatchDeliveryFixture before=null!,after=null!;
    WorkflowMarketConditionFailures journal=null!;
    WorkflowStrategyStateUpdatedEvent[] sources=[];
    [IterationSetup]public void Setup()
    {
        journal=new(Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-market-condition-bdn",Guid.NewGuid().ToString("N")));
        before=new();after=new(marketConditionFailures:journal);
        before.Start().GetAwaiter().GetResult();after.Start().GetAwaiter().GetResult();
        sources=Enumerable.Range(1,Batch).Select(i=>WorkflowDispatchDeliveryFixture.MarketConditionInput(i)).ToArray();
        if(Mode=="CompletedReplay")foreach(var source in sources)
        {if(!before.Deliver(source).GetAwaiter().GetResult()||!after.Deliver(source).GetAwaiter().GetResult())throw new InvalidDataException("Initialization setup failed.");}
    }
    [Benchmark(Baseline=true,OperationsPerInvoke=Batch)]public async Task BeforeVolatileHandoff()
    {foreach(var source in sources)if(!await before.Deliver(source))throw new InvalidDataException("Volatile Market Condition initialization failed.");}
    [Benchmark(OperationsPerInvoke=Batch)]public async Task AfterFrozenTerminal()
    {foreach(var source in sources)if(!await after.Deliver(source))throw new InvalidDataException("Durable Market Condition initialization failed.");}
    [IterationCleanup]public void Cleanup()
    {before.DisposeAsync().GetAwaiter().GetResult();after.DisposeAsync().GetAwaiter().GetResult();journal.Dispose();}
}
