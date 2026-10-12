using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime.Actor;
public interface IFuturesExitPositionWorkflowRealtimeContext : IRealtimeActorContext<FuturesExitPositionWorkflowRealtimeActor>
{
    IDbContextFactory DbFactory { get; }

    TimeProvider TimeProvider { get; }

    ILogger<FuturesExitPositionWorkflowRealtimeActor> Logger { get; }
}
