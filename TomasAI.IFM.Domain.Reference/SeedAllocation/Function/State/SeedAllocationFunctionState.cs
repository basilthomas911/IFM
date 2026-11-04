using TomasAI.IFM.Domain.Reference.Shared.SeedAllocation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.SeedAllocation.Function.State;

public sealed class SeedAllocationFunctionState : BaseEventSourceActorState<SeedAllocationFunctionState>,
    IEventSourceFunctionState<SeedAllocationFunctionState, AllocateSeedIdCommand, SeedAllocationCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public SeedAllocationCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;

    public bool Matches(AllocateSeedIdCommand request) =>
        CompletedEvent?.CommandId == request.CommandId &&
        string.Equals(CompletedEvent.SeedType, request.SeedType, StringComparison.Ordinal);

    public bool TryComplete(SeedAllocationCompletedEvent result, AllocateSeedIdCommand request) =>
        !IsCompleted && Update(result, request);

    protected override bool Apply(IEvent source)
    {
        if (source is not SeedAllocationCompletedEvent result)
            return false;
        CompletedEvent = result;
        return true;
    }
}
