using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime.Actor;
public sealed class FuturesExitPositionWorkflowRealtimeContext : EventActorContext, IFuturesExitPositionWorkflowRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesExitPositionWorkflowRealtimeContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<FuturesExitPositionWorkflowRealtimeActor> logger) : base(supervisor, new(ActorType.Realtime, FuturesExitPositionWorkflowRealtimeActor.ActorName))
    {
        DbFactory = dbFactory;
        Logger = logger;
    }

    public IDbContextFactory DbFactory { get; }
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<FuturesExitPositionWorkflowRealtimeActor> Logger { get; }
}
