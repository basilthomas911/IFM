using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class ResolvePortfolioOrderCompositionExecutionPolicy
{

    extension(EvaluatePortfolioOrderCompositionCommand request)
    {
        public FunctionExecutionPolicy ResolveExecutionPolicy(FunctionFailureStage stage, IPortfolioOrderCompositionFunctionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(request);
            var deadline = stage == FunctionFailureStage.Loading ? context.TimeProvider.GetUtcNow().UtcDateTime.AddSeconds(1) : request.ExpiresAtUtc;
            return new(context.TimeProvider, deadline, FunctionCompletionMode.AtomicBusinessAndEvent);
        }
    }
}
