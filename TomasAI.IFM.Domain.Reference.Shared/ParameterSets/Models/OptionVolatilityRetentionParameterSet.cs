namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Immutable authoring contract for option-volatility history and evidence retention.</summary>
public sealed record OptionVolatilityRetentionParameterSet
{
    public int SchemaVersion { get; init; } = 1;
    public Guid ParameterSetId { get; init; }
    public int Version { get; init; } = 1;
    public int SourceObservationYears { get; init; } = 2;
    public int FinalizedDailyMetricYears { get; init; } = 7;
    public int IntradayCheckpointDays { get; init; } = 90;
    public int DecisionSnapshotYearsAfterCompletion { get; init; } = 7;
    public RetentionDependencyPolicy ReferencedEvidencePolicy { get; init; } = RetentionDependencyPolicy.FollowSnapshotCannotExpireIndependently;
    public RetentionLifetime LatestPointerLifetime { get; init; } = RetentionLifetime.Permanent;
    public RetentionLifetime DefinitionLifetime { get; init; } = RetentionLifetime.Permanent;
    public RetentionLifetime PublishedParameterLifetime { get; init; } = RetentionLifetime.Permanent;
    public CorrectionRetentionPolicy CorrectionPolicy { get; init; } = CorrectionRetentionPolicy.Retain;
    public int BackfillStagingDays { get; init; } = 30;
    public int FailedPublicationDays { get; init; } = 30;
    public RetentionBucket PartitionBucket { get; init; } = RetentionBucket.Monthly;
    public int MaximumPageSize { get; init; } = 256;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    public HistoricalVolatilitySource[] HistoricalSources { get; init; } =
        [HistoricalVolatilitySource.DatabentoQuoteHistory, HistoricalVolatilitySource.IfmReferenceMetadata];
    public TradeIvBackfillPolicy TradeIvBackfill { get; init; } = TradeIvBackfillPolicy.Disallowed;
    public bool AllowUnqualifiedBackfillPublication { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    public OptionVolatilityEnvironment[] IsolatedEnvironments { get; init; } =
        [OptionVolatilityEnvironment.Production, OptionVolatilityEnvironment.Paper, OptionVolatilityEnvironment.Simulation];
}
