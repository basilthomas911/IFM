using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;

/// <summary>Commits business changes and their canonical completion together; ordinary Function saves are forbidden.</summary>
public sealed class CapacityReservationFunctionStateRepository(IPortfolioDbReadContext database, ICapacityReservationStore store)
    : ITransactionalFunctionStateRepository<CapacityReservationFunctionState, ReservePortfolioTradeRiskCommand, CapacityReservationCompletedEvent>
{
    public async ValueTask<CapacityReservationFunctionState> LoadStateAsync(ReservePortfolioTradeRiskCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var state = new CapacityReservationFunctionState { Id = request.Subject.ThreadId };
        var completed = await (database.ReadOperationAsync<CapacityReservationCompletedEvent>(request.PortfolioId, request.OperationId, null, cancellationToken)).ConfigureAwait(false);
        if (completed is not null) state.ReplayEvents(new IEvent[] { completed });
        return state;
    }
    public ValueTask SaveCompletedStateAsync(IFunctionActorContext context, CapacityReservationFunctionState state, ReservePortfolioTradeRiskCommand request, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new InvalidOperationException("Financial completion requires an enlisted business/event transaction."));
    public async ValueTask<CapacityReservationCompletedEvent> CommitAsync(IFunctionActorContext context, ReservePortfolioTradeRiskCommand request, CapacityReservationCompletedEvent candidate, CancellationToken cancellationToken = default)
        => await (store.ReserveAsync(request, CapacityAdmissionModel.ValidateCommitted,
            receipt => CapacityReservationFunctionActor.MapEvent(new(typeof(CapacityReservationCompletedEvent), request, receipt), TimeProvider.System).Completed!, cancellationToken)).ConfigureAwait(false);
}

