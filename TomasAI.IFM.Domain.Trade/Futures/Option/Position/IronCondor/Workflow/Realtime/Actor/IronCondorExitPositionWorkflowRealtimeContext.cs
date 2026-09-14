using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime.Actor;

public interface IIronCondorExitPositionWorkflowRealtimeContext :
    IRealtimeActorContext<IronCondorExitPositionWorkflowRealtimeActor>
{
    IDbContextFactory DbFactory { get; }
    TimeProvider TimeProvider { get; }
    ILogger<IronCondorExitPositionWorkflowRealtimeActor> Logger { get; }
}

public sealed class IronCondorExitPositionWorkflowRealtimeContext : EventActorContext,
    IRealtimeActorContext<IronCondorExitPositionWorkflowRealtimeActor>,
    IIronCondorExitPositionWorkflowRealtimeContext
{
    public IronCondorExitPositionWorkflowRealtimeContext(IActorSupervisor supervisor,
        IDbContextFactory dbFactory, ILogger<IronCondorExitPositionWorkflowRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, IronCondorExitPositionWorkflowRealtimeActor.ActorName))
    {
        DbFactory = dbFactory;
        Logger = logger;
    }

    public IDbContextFactory DbFactory { get; }
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<IronCondorExitPositionWorkflowRealtimeActor> Logger { get; }
}
