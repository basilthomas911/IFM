using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>Reports failed recovery for one dataset without implying that other datasets stopped.</summary>
[MessagePackObject(AllowPrivate = true)]
public record MarketDataDatasetRecoveryFailedEvent : MarketDataFeedStoppedFailEvent
{
    [IgnoreMember] public new const string Verb = "DatasetRecoveryFailed";
    [Key(16)] public string Dataset { get; init; } = "";
    [Key(17)] public string Role { get; init; } = "";
    [Key(18)] public Guid WorkerInstanceId { get; init; }
    [Key(19)] public Guid GenerationId { get; init; }
}
