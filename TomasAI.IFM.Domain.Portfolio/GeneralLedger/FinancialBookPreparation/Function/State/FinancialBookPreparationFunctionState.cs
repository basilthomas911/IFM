using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialBookPreparation.Function.State;

public sealed class FinancialBookPreparationFunctionState : BaseEventSourceActorState<FinancialBookPreparationFunctionState>,
    IEventSourceFunctionState<FinancialBookPreparationFunctionState, PrepareFinancialBookCommand, FinancialBookPreparationCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public FinancialBookPreparationCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;

    public bool Matches(PrepareFinancialBookCommand request) =>
        CompletedEvent?.CommandId == request.CommandId && CompletedEvent.EntityId == request.EntityId
        && string.Equals(CompletedEvent.InputHash, FinancialCanonicalHash.Compute(new { request.Parameters, request.Scope }), StringComparison.Ordinal);

    public bool TryComplete(FinancialBookPreparationCompletedEvent result, PrepareFinancialBookCommand request) =>
        !IsCompleted && Update(result, request);

    protected override bool Apply(IEvent source)
    {
        if (source is not FinancialBookPreparationCompletedEvent result)
            return false;
        CompletedEvent = result;
        return true;
    }
}
