using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function;

/// <summary>Returns classified non-durable refusals; uncertainty never claims that capacity was released.</summary>
public static class FailCapacityConsumption
{
    public static FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent> Fail(this FunctionEventContext<ConsumeCapacityReservationCommand> input,TimeProvider clock)
    {
        var request=input.Request; var error=input.Exception as FinancialOperationException;
        var unknown=input.Exception is FunctionCommitOutcomeUnknownException;
        var code=unknown?FinancialReasons.CommitUnknown:error?.Code??(input.IsConflict?FinancialReasons.RequestMismatch:
            input.Exception is TimeoutException?FinancialReasons.TimeExpired:
            input.Stage is FunctionFailureStage.Parsing or FunctionFailureStage.Validation?FinancialReasons.InvalidContract:FinancialReasons.PersistenceFailed);
        var now=clock.GetUtcNow().UtcDateTime;
        return FunctionResult<CapacityConsumptionCompletedEvent,CapacityConsumptionFailedEvent>.Fail(new()
        {
            Id=Guid.NewGuid(),Subject=new(ActorType.Function,ConsumeCapacityReservationCommand.Actor,nameof(CapacityConsumptionFailedEvent),request?.EntityId.Format()??string.Empty),
            EntityId=request?.EntityId??new(0,Guid.Empty),CommandId=request?.CommandId??Guid.Empty,OperationId=request?.OperationId??Guid.Empty,
            PortfolioId=request?.PortfolioId??0,CorrelationId=request?.CorrelationId??Guid.Empty,CausationId=request?.CausationId??Guid.Empty,
            FailedAtUtc=now,ReceivedOn=now,ErrorCode=code,ReasonCode=FinancialReasons.Name(code),ErrorType=ErrorType.Command,
            FailureClass=input.Stage.ToString(),CommitDisposition=unknown?FinancialCommitDisposition.OutcomeUnknown:error?.Disposition??FinancialCommitDisposition.NotCommitted,
            ExpectedRevision=request?.ExpectedFinancialRevision??0,ExistingOperationId=error?.ExistingOperationId,
            Message=error?.Message??(unknown?"Commit outcome is unknown; recover using the same operation identity.":"Financial operation could not complete."),
            AggregateId=request?.EntityId.Format()??string.Empty,CommandName=nameof(ConsumeCapacityReservationCommand)
        });
    }
}

