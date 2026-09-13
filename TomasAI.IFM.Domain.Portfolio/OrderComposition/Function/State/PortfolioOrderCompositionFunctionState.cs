using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;

public sealed class PortfolioOrderCompositionFunctionState : BaseEventSourceActorState<PortfolioOrderCompositionFunctionState>,
    IEventSourceFunctionState<PortfolioOrderCompositionFunctionState,EvaluatePortfolioOrderCompositionCommand,PortfolioOrderCompositionCompletedEvent>
{
    public override ActorThreadId Id { get; set; }=default!;
    public PortfolioOrderCompositionCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted=>CompletedEvent is not null;
    public bool Matches(EvaluatePortfolioOrderCompositionCommand request)=>CompletedEvent is { } value
        && value.OperationId==request.OperationId && value.PortfolioId==request.PortfolioId && value.InputHash==request.InputSha256;
    public bool TryComplete(PortfolioOrderCompositionCompletedEvent completed,EvaluatePortfolioOrderCompositionCommand request)=>!IsCompleted&&Apply(completed,false);
    protected override bool Apply(IEvent domainEvent)
    {
        if(domainEvent is not PortfolioOrderCompositionCompletedEvent completed)return false;
        CompletedEvent=completed;return true;
    }
}
