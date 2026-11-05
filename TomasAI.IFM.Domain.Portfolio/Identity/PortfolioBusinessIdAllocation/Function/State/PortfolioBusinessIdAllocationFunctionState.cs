using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.State;

public sealed class PortfolioBusinessIdAllocationFunctionState : BaseEventSourceActorState<PortfolioBusinessIdAllocationFunctionState>,
    IEventSourceFunctionState<PortfolioBusinessIdAllocationFunctionState, AllocatePortfolioBusinessIdCommand, PortfolioBusinessIdAllocationCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public PortfolioBusinessIdAllocationCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;

    public bool Matches(AllocatePortfolioBusinessIdCommand request) =>
        CompletedEvent?.CommandId == request.CommandId && CompletedEvent.EntityId == request.EntityId
        && string.Equals(CompletedEvent.InputHash, FinancialCanonicalHash.Compute(new { request.Kind, request.Access }), StringComparison.Ordinal);

    public bool TryComplete(PortfolioBusinessIdAllocationCompletedEvent result, AllocatePortfolioBusinessIdCommand request) =>
        !IsCompleted && Update(result, request);

    protected override bool Apply(IEvent source)
    {
        if (source is not PortfolioBusinessIdAllocationCompletedEvent result)
            return false;
        CompletedEvent = result;
        return true;
    }
}
