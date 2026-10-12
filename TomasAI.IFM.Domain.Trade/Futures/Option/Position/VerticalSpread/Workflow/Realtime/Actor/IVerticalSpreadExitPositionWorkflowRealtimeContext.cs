using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime.Actor;
public interface IVerticalSpreadExitPositionWorkflowRealtimeContext : IRealtimeActorContext<VerticalSpreadExitPositionWorkflowRealtimeActor>
{
    IDbContextFactory DbFactory { get; }

    TimeProvider TimeProvider { get; }

    ILogger<VerticalSpreadExitPositionWorkflowRealtimeActor> Logger { get; }
}
