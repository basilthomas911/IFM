using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FeedSessionPreparation.Function.State;

public sealed class FeedSessionPreparationFunctionState : BaseEventSourceActorState<FeedSessionPreparationFunctionState>,
    IEventSourceFunctionState<FeedSessionPreparationFunctionState, PrepareMarketDataFeedSessionCommand, FeedSessionPreparationCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public FeedSessionPreparationCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;

    public bool Matches(PrepareMarketDataFeedSessionCommand request) =>
        CompletedEvent?.CommandId == request.CommandId && CompletedEvent.EntityId == request.EntityId;

    public bool TryComplete(FeedSessionPreparationCompletedEvent result, PrepareMarketDataFeedSessionCommand request) =>
        !IsCompleted && Update(result, request);

    protected override bool Apply(IEvent source)
    {
        if (source is not FeedSessionPreparationCompletedEvent result)
            return false;
        CompletedEvent = result;
        return true;
    }
}
