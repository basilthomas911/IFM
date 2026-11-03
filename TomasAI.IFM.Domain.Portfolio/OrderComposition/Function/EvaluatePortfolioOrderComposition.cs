using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class EvaluatePortfolioOrderComposition
{

    extension(EvaluatePortfolioOrderCompositionCommand request)
    {
        public ValueTask<FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>> ExecuteAsync(
        IPortfolioOrderCompositionFunctionContext context,
            Func<FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>, FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>> dispatch,
            CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(dispatch);
            token.ThrowIfCancellationRequested();
            FinancialRequestValidation.Demand(request, "OrderCompositionEvaluate", context.TimeProvider.GetUtcNow().UtcDateTime);
            return ValueTask.FromResult(dispatch(new(typeof(PortfolioOrderCompositionCompletedEvent), request, new PortfolioOrderCompositionReceipt())));
        }
    }
}
