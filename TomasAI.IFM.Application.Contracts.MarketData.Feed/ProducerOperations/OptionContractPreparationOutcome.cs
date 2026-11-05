using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;

[MessagePackObject]
public sealed partial record OptionContractPreparationCompletedEvent : FeedOperationComplete<ActorEntityId>
{
    [Key(8)] public FuturesOptionContractReadModel[] Contracts { get; init; } = [];
    [Key(9)] public string? OriginalContractId { get; init; }
    [Key(10)] public string RequestFingerprint { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed partial record OptionContractPreparationFailedEvent : FeedOperationFail<ActorEntityId>;
