using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime.Actor;

public interface IFuturesExitPositionWorkflowRealtimeContext :
    IRealtimeActorContext<FuturesExitPositionWorkflowRealtimeActor>
{
    IDbContextFactory DbFactory { get; }
    TimeProvider TimeProvider { get; }
    ILogger<FuturesExitPositionWorkflowRealtimeActor> Logger { get; }
}

public sealed class FuturesExitPositionWorkflowRealtimeContext : EventActorContext,
    IRealtimeActorContext<FuturesExitPositionWorkflowRealtimeActor>,
    IFuturesExitPositionWorkflowRealtimeContext
{
    public FuturesExitPositionWorkflowRealtimeContext(IActorSupervisor supervisor,
        IDbContextFactory dbFactory, ILogger<FuturesExitPositionWorkflowRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesExitPositionWorkflowRealtimeActor.ActorName))
    {
        DbFactory = dbFactory;
        Logger = logger;
    }

    public IDbContextFactory DbFactory { get; }
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<FuturesExitPositionWorkflowRealtimeActor> Logger { get; }
}
