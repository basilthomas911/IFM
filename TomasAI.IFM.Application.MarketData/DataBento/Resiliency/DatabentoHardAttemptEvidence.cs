namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoHardAttemptEvidence(int Attempt, bool SafeToRetry,
    string Stage, string FailureType, string Detail, IReadOnlyList<int> WorkerProcessIds)
{
    public Guid AttemptId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public TimeSpan Elapsed { get; init; }
    public IReadOnlyDictionary<string, Guid> DatasetGenerations { get; init; } =
        new Dictionary<string, Guid>(StringComparer.Ordinal);
    public string CleanupFailureType { get; init; } = string.Empty;
    public string CleanupDetail { get; init; } = string.Empty;
}
