using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function;

/// <summary>Authorizes execution and maps a candidate; only the transactional repository can produce the committed receipt.</summary>
public static class ExecuteCapacityReservation
{
    public static ValueTask<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>> ExecuteAsync(this ReservePortfolioTradeRiskCommand request,ICapacityReservationFunctionContext context,
        Func<FunctionEventContext<ReservePortfolioTradeRiskCommand>,FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>> dispatch,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        FinancialRequestValidation.Demand(request,"CapacityReserve",context.TimeProvider.GetUtcNow().UtcDateTime);
        return ValueTask.FromResult(dispatch(new(typeof(CapacityReservationCompletedEvent),request,new CapacityReservationReceipt())));
    }
}

