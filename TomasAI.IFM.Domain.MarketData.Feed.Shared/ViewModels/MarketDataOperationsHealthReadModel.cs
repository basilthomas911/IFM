namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>Versioned read-only operations projection; no worker/native handles or recovery authority.</summary>
public sealed record MarketDataOperationsHealthReadModel
{
    public int SchemaVersion { get; init; } = 1;
    public long Revision { get; init; }
    public DateTime ObservedOnUtc { get; init; }
    public string OverallStatus { get; init; } = "Inactive";
    public string SessionState { get; init; } = "Unknown";
    public DateOnly? ValueDate { get; init; }
    public DateTime? LastProbeUtc { get; init; }
    public DateTime? NextProbeUtc { get; init; }
    public long RejectedStaleGenerationPublications { get; init; }
    public IReadOnlyList<MarketDataOperationStageReadModel> Stages { get; init; } = [];
    public IReadOnlyList<MarketDataDatasetHealthReadModel> Datasets { get; init; } = [];
}
