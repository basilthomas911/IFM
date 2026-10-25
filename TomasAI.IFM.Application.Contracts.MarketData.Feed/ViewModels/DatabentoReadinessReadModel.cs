using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

[MessagePackObject(false)]
public sealed record DatabentoReadinessReadModel
{
    [Key(0)] public string State { get; init; } = string.Empty;
    [Key(1)] public string DisplayHealth { get; init; } = string.Empty;
    [Key(2)] public bool CoreReady { get; init; }
    [Key(3)] public DateOnly? ValueDate { get; init; }
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public Guid NativeGeneration { get; init; }
    [Key(6)] public int RecoveryAttempt { get; init; }
    [Key(7)] public string Reason { get; init; } = string.Empty;
    [Key(8)] public DateTime ChangedOnUtc { get; init; }
    [Key(9)] public DateTime? NextRetryOnUtc { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(10)] public DatabentoFeedStatusReadModel[] Feeds { get; init; } = [];
}
