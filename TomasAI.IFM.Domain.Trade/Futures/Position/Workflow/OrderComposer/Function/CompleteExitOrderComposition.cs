using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function;

public static class CompleteExitOrderComposition
{

    extension(FunctionEventContext<ComposeExitOrderCommand> input)
    {
        public FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>
            Complete(
                TimeProvider clock) =>
            StrategyExitFunctionEventMapping.CompleteComposition(
                input, FuturesExitOrderCompositionFunctionActor.ActorName, clock);
    }
}
