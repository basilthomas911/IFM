using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime;

public static class ExitPositionWorkflowStarted
{

    extension(ExitPositionWorkflowStartedEvent started)
    {
        public ValueTask ExecuteAsync(IIronCondorExitPositionWorkflowRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return ValueTask.CompletedTask;
        }
    }
}
