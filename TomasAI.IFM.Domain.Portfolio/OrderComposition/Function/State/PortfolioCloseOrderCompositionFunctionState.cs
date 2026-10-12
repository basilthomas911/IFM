using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;

public sealed class PortfolioCloseOrderCompositionFunctionState :
    BaseEventSourceActorState<PortfolioCloseOrderCompositionFunctionState>,
    IEventSourceFunctionState<PortfolioCloseOrderCompositionFunctionState,
        EvaluatePortfolioCloseOrderCompositionCommand, PortfolioCloseOrderCompositionCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public PortfolioCloseOrderCompositionCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;
    public bool Matches(EvaluatePortfolioCloseOrderCompositionCommand request) 
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompletedEvent is { } completed && completed.OperationId == request.OperationId &&
        completed.PortfolioId == request.PortfolioId && string.Equals(completed.InputHash,request.InputSha256,StringComparison.Ordinal);
    }
    public bool TryComplete(PortfolioCloseOrderCompositionCompletedEvent completed,
        EvaluatePortfolioCloseOrderCompositionCommand request) => !IsCompleted && Apply(completed, false);

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not PortfolioCloseOrderCompositionCompletedEvent completed)
            return false;
        CompletedEvent = completed;
        return true;
    }
}
