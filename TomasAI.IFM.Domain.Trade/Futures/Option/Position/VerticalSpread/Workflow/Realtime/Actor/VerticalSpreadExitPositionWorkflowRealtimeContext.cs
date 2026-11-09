using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime.Actor;
public sealed class VerticalSpreadExitPositionWorkflowRealtimeContext : EventActorContext, IVerticalSpreadExitPositionWorkflowRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public VerticalSpreadExitPositionWorkflowRealtimeContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<VerticalSpreadExitPositionWorkflowRealtimeActor> logger, TimeProvider? timeProvider = null) : base(supervisor, new(ActorType.Realtime, VerticalSpreadExitPositionWorkflowRealtimeActor.ActorName))
    {
        TimeProvider = timeProvider ?? TimeProvider.System;
        DbFactory = dbFactory;
        Logger = logger;
    }

    public IDbContextFactory DbFactory { get; }
    public TimeProvider TimeProvider { get; }
    public ILogger<VerticalSpreadExitPositionWorkflowRealtimeActor> Logger { get; }
}
