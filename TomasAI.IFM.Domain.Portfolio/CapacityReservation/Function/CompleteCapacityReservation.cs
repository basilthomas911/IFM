using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function;

/// <summary>Maps a candidate or the canonical committed/replayed financial receipt.</summary>
public static class CompleteCapacityReservation
{
    public static FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent> Complete(this FunctionEventContext<ReservePortfolioTradeRiskCommand> input)
    {
        if(input.Outcome is CapacityReservationCompletedEvent committed) return FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>.Complete(committed);
        var request=input.Request??throw new InvalidOperationException("Completion requires a request.");
        var receipt=input.Outcome as CapacityReservationReceipt??throw new InvalidOperationException("Completion requires a typed receipt.");
        return FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>.Complete(new()
        {
            Id=receipt.CompletedEventId,Subject=new(ActorType.Function,ReservePortfolioTradeRiskCommand.Actor,nameof(CapacityReservationCompletedEvent),request.EntityId.Format()),
            EntityId=request.EntityId,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,
            CorrelationId=request.CorrelationId,CausationId=request.CausationId,CommittedAtUtc=receipt.GrantedAtUtc,ReceivedOn=receipt.GrantedAtUtc,
            InputHash=request.InputSha256,Receipt=receipt,AggregateId=request.EntityId.Format()
        });
    }
}

