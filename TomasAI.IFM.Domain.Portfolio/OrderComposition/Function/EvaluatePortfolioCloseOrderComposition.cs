using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class EvaluatePortfolioCloseOrderComposition
{
    public static ValueTask<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
        PortfolioCloseOrderCompositionFailedEvent>> ExecuteAsync(
        this EvaluatePortfolioCloseOrderCompositionCommand request,
        IPortfolioCloseOrderCompositionFunctionContext context,
        Func<FunctionEventContext<EvaluatePortfolioCloseOrderCompositionCommand>,
            FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
                PortfolioCloseOrderCompositionFailedEvent>> dispatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FinancialRequestValidation.Demand(
            request, "OrderCompositionClose", context.TimeProvider.GetUtcNow().UtcDateTime);
        return ValueTask.FromResult(dispatch(new(
            typeof(PortfolioCloseOrderCompositionCompletedEvent), request,
            new PortfolioCloseOrderCompositionReceipt())));
    }
}
