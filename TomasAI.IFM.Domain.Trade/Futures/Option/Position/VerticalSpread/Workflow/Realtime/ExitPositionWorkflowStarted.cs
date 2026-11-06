using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime;

public static class ExitPositionWorkflowStarted
{

    extension(ExitPositionWorkflowStartedEvent started)
    {
        public ValueTask ExecuteAsync(IVerticalSpreadExitPositionWorkflowRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return ValueTask.CompletedTask;
        }
    }
}
