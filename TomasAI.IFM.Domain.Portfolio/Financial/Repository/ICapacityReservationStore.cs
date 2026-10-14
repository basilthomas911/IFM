


using TomasAI.IFM.Domain.Portfolio.Shared.Financial;





namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

public interface ICapacityReservationStore
{
    Task<CapacityReservationCompletedEvent> ReserveAsync(ReservePortfolioTradeRiskCommand request,
        CapacityAdmissionCalculation validate, Func<CapacityReservationReceipt, CapacityReservationCompletedEvent> complete, CancellationToken token = default);
    Task<T> ChangeAsync<T>(IFinancialRequest<CapacityLifecycleRequest> request, bool consumption,
        CapacityLifecycleCalculation calculate, Func<CapacityLifecycleReceipt, T> complete, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent;
    Task<ReservationSnapshot?> ReadCurrentAsync(int portfolioId, Guid reservationId, CancellationToken token = default);
}
