using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class ResolvePortfolioOrderCompositionExecutionPolicy
{
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this EvaluatePortfolioOrderCompositionCommand request,
        FunctionFailureStage stage,IPortfolioOrderCompositionFunctionContext context)
    {
        var deadline=stage==FunctionFailureStage.Loading?context.TimeProvider.GetUtcNow().UtcDateTime.AddSeconds(1):request.ExpiresAtUtc;
        return new(context.TimeProvider,deadline,FunctionCompletionMode.AtomicBusinessAndEvent);
    }
}
