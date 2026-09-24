using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class EvaluatePortfolioOrderComposition
{
    public static ValueTask<FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>> ExecuteAsync(
        this EvaluatePortfolioOrderCompositionCommand request,IPortfolioOrderCompositionFunctionContext context,
        Func<FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>,FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>> dispatch,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        FinancialRequestValidation.Demand(request,"OrderCompositionEvaluate",context.TimeProvider.GetUtcNow().UtcDateTime);
        return ValueTask.FromResult(dispatch(new(typeof(PortfolioOrderCompositionCompletedEvent),request,new PortfolioOrderCompositionReceipt())));
    }
}
