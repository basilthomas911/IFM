using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface ICapacityReservationStore
{
    Task<CapacityReservationCompletedEvent> ReserveAsync(ReservePortfolioTradeRiskCommand request,
        CapacityAdmissionCalculation validate, Func<CapacityReservationReceipt, CapacityReservationCompletedEvent> complete, CancellationToken token = default);
    Task<T> ChangeAsync<T>(IFinancialRequest<CapacityLifecycleRequest> request, bool consumption,
        CapacityLifecycleCalculation calculate, Func<CapacityLifecycleReceipt, T> complete, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent;
    Task<ReservationSnapshot?> ReadCurrentAsync(int portfolioId, Guid reservationId, CancellationToken token = default);
}
