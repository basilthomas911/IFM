using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Realtime;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Realtime;

public static class ExitPositionWorkflowStarted
{
    public static ValueTask ExecuteAsync(this ExitPositionWorkflowStartedEvent started,
        IVerticalSpreadExitPositionWorkflowRealtimeContext context) =>
        StrategyExitWorkflowExecution.ExecuteAsync(
            started, context, context.TimeProvider, context.DbFactory.TradePlanDb.DbWriter);
}
