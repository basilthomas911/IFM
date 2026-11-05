using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Preparation.Function.State;

public sealed class OptionContractPreparationFunctionState : BaseEventSourceActorState<OptionContractPreparationFunctionState>,
    IEventSourceFunctionState<OptionContractPreparationFunctionState, PrepareOptionContractsCommand, OptionContractPreparationCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public OptionContractPreparationCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;

    public bool Matches(PrepareOptionContractsCommand request) =>
        CompletedEvent?.CommandId == request.CommandId && CompletedEvent.EntityId == request.EntityId
        && string.Equals(CompletedEvent.RequestFingerprint, request.GetFingerprint(), StringComparison.Ordinal);

    public bool TryComplete(OptionContractPreparationCompletedEvent result, PrepareOptionContractsCommand request) =>
        !IsCompleted && Update(result, request);

    protected override bool Apply(IEvent source)
    {
        if (source is not OptionContractPreparationCompletedEvent result)
            return false;
        CompletedEvent = result;
        return true;
    }
}
