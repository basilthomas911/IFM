using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function;

public static class ComposeExitOrder
{

    extension(ComposeExitOrderCommand command)
    {
        public ValueTask<FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>> ExecuteAsync(ExitOrderCompositionFunctionState state, TimeProvider timeProvider, Func<FunctionEventContext<ComposeExitOrderCommand>, FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>> dispatch, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(dispatch);
            ArgumentNullException.ThrowIfNull(timeProvider);
            cancellationToken.ThrowIfCancellationRequested();
            if (command.StrategyKind != TradeStrategyKind.FuturesOutright)
                throw new InvalidOperationException("EXIT.COMPOSITION.FUTURES_STRATEGY_REQUIRED");
            var composition = StrategyExitOrderCompositionModel.Compose(command.Started, timeProvider.GetUtcNow().UtcDateTime);
            return ValueTask.FromResult(dispatch(new FunctionEventContext<ComposeExitOrderCommand>(typeof(ExitOrderCompositionCompletedEvent), command, composition)));
        }
    }
}
