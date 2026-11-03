using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function;

public static class ResolveExitOrderCompositionExecutionPolicy
{

    extension(ComposeExitOrderCommand command)
    {
        public FunctionExecutionPolicy ResolveExecutionPolicy(
            FunctionFailureStage stage,
            TimeProvider clock) =>
            new(clock, null);
    }
}
