using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function.State;

public sealed class StreamingRequestIdAllocationFunctionState : BaseEventSourceActorState<StreamingRequestIdAllocationFunctionState>,
    IEventSourceFunctionState<StreamingRequestIdAllocationFunctionState, AllocateStreamingRequestIdCommand, StreamingRequestIdAllocationCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public StreamingRequestIdAllocationCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;

    public bool Matches(AllocateStreamingRequestIdCommand request) =>
        CompletedEvent?.CommandId == request.CommandId && CompletedEvent.EntityId == request.EntityId;

    public bool TryComplete(StreamingRequestIdAllocationCompletedEvent result, AllocateStreamingRequestIdCommand request) =>
        !IsCompleted && Update(result, request);

    protected override bool Apply(IEvent source)
    {
        if (source is not StreamingRequestIdAllocationCompletedEvent result)
            return false;
        CompletedEvent = result;
        return true;
    }
}
