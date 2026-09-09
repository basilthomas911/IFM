using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;

/// <summary>Commits business changes and their canonical completion together; ordinary Function saves are forbidden.</summary>
public sealed class CapacityConsumptionFunctionStateRepository(IPortfolioFinancialDbContext database,ICapacityReservationStore store)
    : ITransactionalFunctionStateRepository<CapacityConsumptionFunctionState,ConsumeCapacityReservationCommand,CapacityConsumptionCompletedEvent>
{
    public async ValueTask<CapacityConsumptionFunctionState> LoadStateAsync(ConsumeCapacityReservationCommand request,CancellationToken cancellationToken=default)
    {
        var state=new CapacityConsumptionFunctionState { Id=request.Subject.ThreadId };
        var completed=await database.ReadOperationAsync<CapacityConsumptionCompletedEvent>(request.PortfolioId,request.OperationId,null,cancellationToken);
        if(completed is not null) state.ReplayEvents(new IEvent[] { completed });
        return state;
    }
    public ValueTask SaveCompletedStateAsync(IFunctionActorContext context,CapacityConsumptionFunctionState state,ConsumeCapacityReservationCommand request,CancellationToken cancellationToken=default)
        =>ValueTask.FromException(new InvalidOperationException("Financial completion requires an enlisted business/event transaction."));
    public async ValueTask<CapacityConsumptionCompletedEvent> CommitAsync(IFunctionActorContext context,ConsumeCapacityReservationCommand request,CapacityConsumptionCompletedEvent candidate,CancellationToken cancellationToken=default)
        =>await store.ChangeAsync(request, true, CapacityLifecycleModel.Apply,
            receipt => CapacityConsumptionFunctionActor.MapEvent(new(typeof(CapacityConsumptionCompletedEvent),request,receipt),TimeProvider.System).Completed!, cancellationToken);
}

