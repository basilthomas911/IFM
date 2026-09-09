using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;

/// <summary>Immutable original completion for one financial operation; current capacity is queried separately.</summary>
public sealed class CapacityConsumptionFunctionState : BaseEventSourceActorState<CapacityConsumptionFunctionState>,
    IEventSourceFunctionState<CapacityConsumptionFunctionState,ConsumeCapacityReservationCommand,CapacityConsumptionCompletedEvent>
{
    public override ActorThreadId Id { get; set; }=default!;
    public CapacityConsumptionCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted=>CompletedEvent is not null;
    public bool Matches(ConsumeCapacityReservationCommand request)=>CompletedEvent is { } completed && completed.OperationId==request.OperationId &&
        completed.PortfolioId==request.PortfolioId && completed.InputHash==request.InputSha256;
    public bool TryComplete(CapacityConsumptionCompletedEvent completed,ConsumeCapacityReservationCommand request)=>!IsCompleted && Apply(completed,false);
    protected override bool Apply(IEvent domainEvent)
    {
        if(domainEvent is not CapacityConsumptionCompletedEvent completed) return false;
        CompletedEvent=completed;
        return true;
    }
}

