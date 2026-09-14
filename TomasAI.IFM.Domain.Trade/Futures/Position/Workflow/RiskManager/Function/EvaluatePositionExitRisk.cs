using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function.State;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function;

public static class EvaluatePositionExitRisk
{
    public static async ValueTask<FunctionResult<PositionExitRiskCompletedEvent,
        ExitPositionWorkflowFailedEvent>> ExecuteAsync(
        this EvaluatePositionExitRiskCommand command,
        PositionExitRiskFunctionState state,
        IPortfolioOrderCompositionApi portfolio,
        TimeProvider timeProvider,
        Func<FunctionEventContext<EvaluatePositionExitRiskCommand>,
            FunctionResult<PositionExitRiskCompletedEvent,
                ExitPositionWorkflowFailedEvent>> dispatch,
        CancellationToken cancellationToken)
    {
        if (command.StrategyKind != TradeStrategyKind.FuturesOutright)
            throw new InvalidOperationException("EXIT.RISK.FUTURES_STRATEGY_REQUIRED");

        var decision = await PositionExitRiskModel.EvaluateAsync(
            command, portfolio, timeProvider, cancellationToken).ConfigureAwait(false);
        return dispatch(new FunctionEventContext<EvaluatePositionExitRiskCommand>(
            typeof(PositionExitRiskCompletedEvent), command, decision));
    }
}
