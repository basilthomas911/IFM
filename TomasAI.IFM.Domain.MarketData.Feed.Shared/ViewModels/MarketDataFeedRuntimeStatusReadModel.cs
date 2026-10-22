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

    [Key(3)] public string? OptionFeedError { get; init; }
    [Key(4)] public bool OptionCleanupConfirmed { get; init; }
    [Key(5)] public Guid OptionWorkerInstanceId { get; init; }
    [Key(6)] public Guid OptionGenerationId { get; init; }
    [Key(7)] public string? OptionDataset { get; init; }
    [Key(8)] public DateTime? OptionFailureUtc { get; init; }

    [IgnoreMember]
    public bool IsValid => ObservedAtUtc != default
        && (IsRunning ? ActiveValueDate.HasValue : !ActiveValueDate.HasValue);
}
