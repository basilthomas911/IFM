using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class CompletePortfolioOrderComposition
{
    public static FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent> Complete(
        this FunctionEventContext<EvaluatePortfolioOrderCompositionCommand> input,TimeProvider clock)
    {
        if(input.Outcome is PortfolioOrderCompositionCompletedEvent committed)
            return FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>.Complete(committed);
        var request=input.Request??throw new InvalidOperationException("Completion requires a request.");
        var now=clock.GetUtcNow().UtcDateTime;
        return FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>.Complete(new()
        {
            Id=Guid.NewGuid(),Subject=request.Subject,EntityId=request.EntityId,CommandId=request.CommandId,
            OperationId=request.OperationId,PortfolioId=request.PortfolioId,CorrelationId=request.CorrelationId,
            CausationId=request.CausationId,CommittedAtUtc=now,ReceivedOn=now,InputHash=request.InputSha256,
            Receipt=input.Outcome as PortfolioOrderCompositionReceipt??new(),AggregateId=request.EntityId.Format()
        });
    }
}
