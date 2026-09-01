using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Provider-neutral point-in-time status of the application-owned market-data feed.
/// </summary>
[MessagePackObject(false)]
public sealed record MarketDataFeedRuntimeStatusReadModel
{
    [Key(0)] public bool IsRunning { get; init; }
    [Key(1)] public DateOnly? ActiveValueDate { get; init; }
    [Key(2)] public DateTimeOffset ObservedAtUtc { get; init; }

    [IgnoreMember]
    public bool IsValid => ObservedAtUtc != default
        && (IsRunning ? ActiveValueDate.HasValue : !ActiveValueDate.HasValue);
}
