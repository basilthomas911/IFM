using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime;

public static class ExitPositionWorkflowStarted
{

    extension(ExitPositionWorkflowStartedEvent started)
    {
        public ValueTask ExecuteAsync(IFuturesExitPositionWorkflowRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return ValueTask.CompletedTask;
        }
    }
}
