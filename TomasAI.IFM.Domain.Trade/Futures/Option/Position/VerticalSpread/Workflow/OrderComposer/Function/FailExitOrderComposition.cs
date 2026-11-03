using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function;

public static class FailExitOrderComposition
{

    extension(FunctionEventContext<ComposeExitOrderCommand> input)
    {
        public FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>
            Fail(
                TimeProvider clock) =>
            StrategyExitFunctionEventMapping.FailComposition(
                input, VerticalSpreadExitOrderCompositionFunctionActor.ActorName, clock);
    }
}
