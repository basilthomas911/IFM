using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;

/// <summary>Original default failure-settlement behavior; same public workflow handler, NullLogger only.</summary>
public sealed class LegacyWorkflowDispatchActor(IIntrinsicTimeStrategyWorkflowRealtimeContext services)
    : BaseEventActor<IntrinsicTimeStrategyWorkflowRealtimeActor>(services,services.Logger)
{
    readonly IntrinsicTimeStrategyWorkflowRealtimeActor handler=new(services);
    protected override ValueTask OnStartup(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)=>ValueTask.CompletedTask;
    protected override ValueTask OnShutdown(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)=>ValueTask.CompletedTask;
    protected override IEvent ParseMessage(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,IActorMessage message)
        => message.AsEvent<WorkflowStrategyStateUpdatedEvent>()!;
    protected override ValueTask ReceiveAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,IEvent source)
        => ((WorkflowStrategyStateUpdatedEvent)source).ExecuteAsync(handler,context);
    // The original failure hook only logs. Both benchmark loggers are disabled.
    protected override ValueTask OnExceptionAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,ActorThreadId thread,IEvent source,Exception error)=>ValueTask.CompletedTask;
}
