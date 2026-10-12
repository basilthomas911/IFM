using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime.Actor;
public interface IIronCondorExitPositionWorkflowRealtimeContext : IRealtimeActorContext<IronCondorExitPositionWorkflowRealtimeActor>
{
    IDbContextFactory DbFactory { get; }

    TimeProvider TimeProvider { get; }

    ILogger<IronCondorExitPositionWorkflowRealtimeActor> Logger { get; }
}
