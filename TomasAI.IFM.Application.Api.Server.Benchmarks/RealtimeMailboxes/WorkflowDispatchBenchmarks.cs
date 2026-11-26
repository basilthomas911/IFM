using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class WorkflowDispatchBenchmarks
{
    [Params("ReadFailure","CommandFailure","TypedRejection","Success")] public string Mode{get;set;}="Success";
    WorkflowDispatchDeliveryFixture before=null!,after=null!;
    WorkflowStrategyStateUpdatedEvent source=null!;
    [GlobalSetup] public async Task Setup()
    {
        source=WorkflowDispatchDeliveryFixture.Input();
        before=new(factory:context=>new LegacyWorkflowDispatchActor(context));after=new();
        foreach(var state in new[]{before,after})
        {
            state.ReadCurrent=_=>Mode=="ReadFailure"?Task.FromException<IntrinsicTimeStrategyWorkflowView?>(new IOException("Read failed.")):Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(source.State);
            if(Mode=="CommandFailure")state.Send=_=>throw new IOException("Command handoff failed.");
            state.TypedRejected=Mode=="TypedRejection";
            await state.Start();
        }
        if(!await before.Deliver(source)||await after.Deliver(source)!=(Mode=="Success"))throw new InvalidDataException("Unexpected workflow delivery result.");
    }
    [Benchmark(Baseline=true)] public Task<bool> BeforeMetricsDependent()=>before.Deliver(source);
    [Benchmark] public Task<bool> AfterRequiredSettlement()=>after.Deliver(source);
    [IterationCleanup] public void Reset(){before.Reset();after.Reset();}
    [GlobalCleanup] public async Task Cleanup(){await before.DisposeAsync();await after.DisposeAsync();}
}
