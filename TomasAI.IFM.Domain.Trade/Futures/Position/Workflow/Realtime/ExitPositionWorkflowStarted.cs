using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Realtime;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Realtime;

public static class ExitPositionWorkflowStarted
{
    public static ValueTask ExecuteAsync(this ExitPositionWorkflowStartedEvent started,
        IFuturesExitPositionWorkflowRealtimeContext context) =>
        StrategyExitWorkflowExecution.ExecuteAsync(
            started, context, context.TimeProvider, context.DbFactory.TradePlanDb.DbWriter);
}
