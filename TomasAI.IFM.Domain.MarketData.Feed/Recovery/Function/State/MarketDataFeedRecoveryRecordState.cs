using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.State;
/// <summary>Only successful storage is cached. Attempt IDs/deadlines do not change immutable matching.</summary>
public sealed class MarketDataFeedRecoveryRecordState : BaseEventSourceActorState<MarketDataFeedRecoveryRecordState>,
    IEventSourceFunctionState<MarketDataFeedRecoveryRecordState, RecordMarketDataFeedRecoveryCommand, MarketDataFeedRecoveryRecordStoredEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public MarketDataFeedRecoveryRecordStoredEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;
    public bool Matches(RecordMarketDataFeedRecoveryCommand request) => CompletedEvent?.RecordId == request.EntityId.Value && CompletedEvent.Digest == request.Digest;
    public bool TryComplete(MarketDataFeedRecoveryRecordStoredEvent result, RecordMarketDataFeedRecoveryCommand request) => !IsCompleted && Update(result, request);
    protected override bool Apply(IEvent source)
    {
        if (source is not MarketDataFeedRecoveryRecordStoredEvent value) return false;
        CompletedEvent = value;
        return true;
    }
}
