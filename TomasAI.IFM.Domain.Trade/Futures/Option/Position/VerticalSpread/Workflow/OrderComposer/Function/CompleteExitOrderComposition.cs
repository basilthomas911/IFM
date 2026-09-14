using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function;

public static class CompleteExitOrderComposition
{
    public static FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>
        Complete(
            this FunctionEventContext<ComposeExitOrderCommand> input,
            TimeProvider clock) =>
        StrategyExitFunctionEventMapping.CompleteComposition(
            input, VerticalSpreadExitOrderCompositionFunctionActor.ActorName, clock);
}
