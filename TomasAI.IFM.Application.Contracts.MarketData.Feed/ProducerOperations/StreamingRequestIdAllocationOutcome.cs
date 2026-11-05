using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;

[MessagePackObject]
public sealed partial record StreamingRequestIdAllocationCompletedEvent : FeedOperationComplete<ActorEntityId>
{
    [Key(8)] public int Value { get; init; }
}

[MessagePackObject]
public sealed partial record StreamingRequestIdAllocationFailedEvent : FeedOperationFail<ActorEntityId>;
