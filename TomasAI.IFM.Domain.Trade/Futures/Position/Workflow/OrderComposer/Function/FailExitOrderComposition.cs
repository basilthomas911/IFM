using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function;

public static class FailExitOrderComposition
{
    public static FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>
        Fail(
            this FunctionEventContext<ComposeExitOrderCommand> input,
            TimeProvider clock) =>
        StrategyExitFunctionEventMapping.FailComposition(
            input, FuturesExitOrderCompositionFunctionActor.ActorName, clock);
}
