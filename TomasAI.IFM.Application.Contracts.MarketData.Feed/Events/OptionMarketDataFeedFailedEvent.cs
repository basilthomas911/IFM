using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>Reports terminal option-dataset containment without stopping the futures session.</summary>
[MessagePackObject(AllowPrivate = true)]
public record OptionMarketDataFeedFailedEvent : MarketDataFeedStoppedFailEvent
{
    [IgnoreMember] public new const string Verb = "OptionFeedFailed";
    [Key(16)] public string Dataset { get; init; } = "";
    [Key(17)] public Guid WorkerInstanceId { get; init; }
    [Key(18)] public Guid GenerationId { get; init; }
    [Key(19)] public bool CleanupConfirmed { get; init; }
}
