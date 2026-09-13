using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class FailPortfolioOrderComposition
{
    public static FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent> Fail(
        this FunctionEventContext<EvaluatePortfolioOrderCompositionCommand> input,TimeProvider clock)
    {
        var request=input.Request;var now=clock.GetUtcNow().UtcDateTime;
        var detail=input.Exception?.ToString()??(input.IsConflict?"A completed operation exists with different input.":"Portfolio order composition failed.");
        return FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>.Fail(new()
        {
            Id=Guid.NewGuid(),Subject=new(ActorType.Function,EvaluatePortfolioOrderCompositionCommand.Actor,nameof(PortfolioOrderCompositionFailedEvent),request?.EntityId.Format()??string.Empty),
            EntityId=request?.EntityId??new(0,Guid.Empty),CommandId=request?.CommandId??Guid.Empty,ErrorDate=now,ReceivedOn=now,
            ErrorCode=request?.ErrorCode??34130,ErrorMessage=input.Exception?.Message??"Portfolio order composition failed.",
            ErrorType=ErrorType.Command,ErrorData=detail,CommandName=nameof(EvaluatePortfolioOrderCompositionCommand),
            AggregateId=request?.EntityId.Format()??string.Empty
        });
    }
}
