using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Realtime;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Realtime;

public static class ExitPositionWorkflowStarted
{

    extension(ExitPositionWorkflowStartedEvent started)
    {
        public ValueTask ExecuteAsync(IIronCondorExitPositionWorkflowRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return StrategyExitWorkflowExecution.ExecuteAsync(started, context, context.TimeProvider, context.DbFactory.TradePlanDb.DbWriter);
        }
    }
}
