using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function;

public static class ComposeExitOrder
{
    public static ValueTask<FunctionResult<ExitOrderCompositionCompletedEvent,
        ExitPositionWorkflowFailedEvent>> ExecuteAsync(
        this ComposeExitOrderCommand command,
        ExitOrderCompositionFunctionState state,
        TimeProvider timeProvider,
        Func<FunctionEventContext<ComposeExitOrderCommand>,
            FunctionResult<ExitOrderCompositionCompletedEvent,
                ExitPositionWorkflowFailedEvent>> dispatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command.StrategyKind != TradeStrategyKind.VerticalSpread)
            throw new InvalidOperationException("EXIT.COMPOSITION.VERTICAL_SPREAD_STRATEGY_REQUIRED");

        var composition = StrategyExitOrderCompositionModel.Compose(
            command.Started, timeProvider.GetUtcNow().UtcDateTime);
        return ValueTask.FromResult(dispatch(
            new FunctionEventContext<ComposeExitOrderCommand>(
                typeof(ExitOrderCompositionCompletedEvent), command, composition)));
    }
}
