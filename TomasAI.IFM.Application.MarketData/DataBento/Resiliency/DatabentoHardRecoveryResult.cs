namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoHardRecoveryResult(Guid CorrelationId, Guid GenerationId,
    int Attempts, DatabentoHardRecoveryOutcome Outcome, string FailedStage, string Detail)
{
    public IReadOnlyDictionary<string, Guid> DatasetGenerations { get; init; } =
        new Dictionary<string, Guid>(StringComparer.Ordinal);
    public IReadOnlyList<DatabentoHardAttemptEvidence> AttemptEvidence { get; init; } = [];
    public IReadOnlyList<string> ContributingReasons { get; init; } = [];
}
