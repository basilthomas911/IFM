using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime.Actor;
public sealed class IronCondorExitPositionWorkflowRealtimeContext : EventActorContext, IIronCondorExitPositionWorkflowRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public IronCondorExitPositionWorkflowRealtimeContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<IronCondorExitPositionWorkflowRealtimeActor> logger) : base(supervisor, new(ActorType.Realtime, IronCondorExitPositionWorkflowRealtimeActor.ActorName))
    {
        DbFactory = dbFactory;
        Logger = logger;
    }

    public IDbContextFactory DbFactory { get; }
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<IronCondorExitPositionWorkflowRealtimeActor> Logger { get; }
}
