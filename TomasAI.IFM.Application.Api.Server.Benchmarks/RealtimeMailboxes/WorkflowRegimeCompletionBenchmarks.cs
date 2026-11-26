using BenchmarkDotNet.Attributes;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class WorkflowRegimeCompletionBenchmarks
{
    const int Batch=16;
    [Params("New","CompletedReplay")]public string Mode{get;set;}="New";
    [Params("Small","Representative")]public string Payload{get;set;}="Small";
    WorkflowDispatchDeliveryFixture before=null!,after=null!;
    WorkflowRegimeFunctionFailures journal=null!;
    WorkflowStrategyStateUpdatedEvent[] sources=[];
    IntrinsicTimeStrategyWorkflowView? current;
    [IterationSetup]public void Setup()
    {
        journal=new(Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-bdn",Guid.NewGuid().ToString("N")));
        before=new();after=new(regimeFunctionFailures:journal);
        before.ReadCurrent=_=>Task.FromResult(current);after.ReadCurrent=before.ReadCurrent;
        before.Start().GetAwaiter().GetResult();after.Start().GetAwaiter().GetResult();
        sources=Enumerable.Range(1,Batch).Select(i=>
        {
            if(Payload=="Small")return WorkflowDispatchDeliveryFixture.RegimeFunctionInput(i);
            var original=MessagePackSerializer.Deserialize<WorkflowStrategyStateUpdatedEvent>(File.ReadAllBytes("C:/repos/IFM/.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-real-source.msgpack"));
            var workflow=new TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId(new Guid(i+600,0,0,new byte[8]));
            return original with{Id=new Guid(i,0,0,new byte[8]),WorkflowId=workflow,State=original.State with{WorkflowId=workflow}};
        }).ToArray();
        before.BindRegimeCompletionInputs(sources[0]);after.BindRegimeCompletionInputs(sources[0]);
        before.Time.At=sources[0].UpdatedAtUtc;after.Time.At=sources[0].UpdatedAtUtc;
        if(Mode=="CompletedReplay")foreach(var source in sources)
        {current=source.State;if(!before.Deliver(source).GetAwaiter().GetResult()||!after.Deliver(source).GetAwaiter().GetResult())throw new InvalidDataException("Function setup failed.");}
    }
    [Benchmark(Baseline=true,OperationsPerInvoke=Batch)]public async Task BeforeVolatileHandoff()
    {foreach(var source in sources){current=source.State;if(!await before.Deliver(source))throw new InvalidDataException("Volatile Function handoff failed.");}}
    [Benchmark(OperationsPerInvoke=Batch)]public async Task AfterFrozenTerminal()
    {foreach(var source in sources){current=source.State;if(!await after.Deliver(source))throw new InvalidDataException("Frozen Function handoff failed.");}}
    [IterationCleanup]public void Cleanup()
    {before.DisposeAsync().GetAwaiter().GetResult();after.DisposeAsync().GetAwaiter().GetResult();journal.Dispose();}
}
