using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;

[MessagePackObject]
public sealed partial record FeedSessionPreparationCompletedEvent : FeedOperationComplete<ActorEntityId>
{
    [Key(8)] public DateOnly ValueDate { get; init; }
    [Key(9)] public FuturesContractV3ReadModel[] Contracts { get; init; } = [];
}

[MessagePackObject]
public sealed partial record FeedSessionPreparationFailedEvent : FeedOperationFail<ActorEntityId>;
