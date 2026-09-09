using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function;

/// <summary>Maps a candidate or the canonical committed/replayed financial receipt.</summary>
public static class CompleteCapacityConsumption
{
    public static FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent> Complete(this FunctionEventContext<ConsumeCapacityReservationCommand> input)
    {
        if(input.Outcome is CapacityConsumptionCompletedEvent committed) return FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent>.Complete(committed);
        var request=input.Request??throw new InvalidOperationException("Completion requires a request.");
        var receipt=input.Outcome as CapacityLifecycleReceipt??throw new InvalidOperationException("Completion requires a typed receipt.");
        return FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent>.Complete(new()
        {
            Id=receipt.CompletedEventId,Subject=new(ActorType.Function,ConsumeCapacityReservationCommand.Actor,nameof(CapacityConsumptionCompletedEvent),request.EntityId.Format()),
            EntityId=request.EntityId,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,
            CorrelationId=request.CorrelationId,CausationId=request.CausationId,CommittedAtUtc=receipt.CommittedAtUtc,ReceivedOn=receipt.CommittedAtUtc,
            InputHash=request.InputSha256,Receipt=receipt,AggregateId=request.EntityId.Format()
        });
    }
}

