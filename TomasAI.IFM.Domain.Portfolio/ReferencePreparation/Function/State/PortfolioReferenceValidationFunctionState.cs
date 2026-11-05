using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.ReferencePreparation.Function.State;

public sealed class PortfolioReferenceValidationFunctionState : BaseEventSourceActorState<PortfolioReferenceValidationFunctionState>,
    IEventSourceFunctionState<PortfolioReferenceValidationFunctionState, ValidatePortfolioReferenceCommand, PortfolioReferenceValidationCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public PortfolioReferenceValidationCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;

    public bool Matches(ValidatePortfolioReferenceCommand request) =>
        CompletedEvent?.CommandId == request.CommandId && CompletedEvent.EntityId == request.EntityId
        && string.Equals(CompletedEvent.InputHash, request.ComputeInputHash(), StringComparison.Ordinal);

    public bool TryComplete(PortfolioReferenceValidationCompletedEvent result, ValidatePortfolioReferenceCommand request) =>
        !IsCompleted && Update(result, request);

    protected override bool Apply(IEvent source)
    {
        if (source is not PortfolioReferenceValidationCompletedEvent result)
            return false;
        CompletedEvent = result;
        return true;
    }
}
