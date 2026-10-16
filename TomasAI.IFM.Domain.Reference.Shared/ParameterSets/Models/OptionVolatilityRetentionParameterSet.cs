namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Immutable authoring contract for option-volatility history and evidence retention.</summary>
[MessagePack.MessagePackObject]
public sealed record OptionVolatilityRetentionParameterSet
{
    [MessagePack.Key(0)] public int SchemaVersion { get; init; } = 1;
    [MessagePack.Key(1)] public Guid ParameterSetId { get; init; }
    [MessagePack.Key(2)] public int Version { get; init; } = 1;
    [MessagePack.Key(3)] public int SourceObservationYears { get; init; } = 2;
    [MessagePack.Key(4)] public int FinalizedDailyMetricYears { get; init; } = 7;
    [MessagePack.Key(5)] public int IntradayCheckpointDays { get; init; } = 90;
    [MessagePack.Key(6)] public int DecisionSnapshotYearsAfterCompletion { get; init; } = 7;
    [MessagePack.Key(7)] public RetentionDependencyPolicy ReferencedEvidencePolicy { get; init; } = RetentionDependencyPolicy.FollowSnapshotCannotExpireIndependently;
    [MessagePack.Key(8)] public RetentionLifetime LatestPointerLifetime { get; init; } = RetentionLifetime.Permanent;
    [MessagePack.Key(9)] public RetentionLifetime DefinitionLifetime { get; init; } = RetentionLifetime.Permanent;
    [MessagePack.Key(10)] public RetentionLifetime PublishedParameterLifetime { get; init; } = RetentionLifetime.Permanent;
    [MessagePack.Key(11)] public CorrectionRetentionPolicy CorrectionPolicy { get; init; } = CorrectionRetentionPolicy.Retain;
    [MessagePack.Key(12)] public int BackfillStagingDays { get; init; } = 30;
    [MessagePack.Key(13)] public int FailedPublicationDays { get; init; } = 30;
    [MessagePack.Key(14)] public RetentionBucket PartitionBucket { get; init; } = RetentionBucket.Monthly;
    [MessagePack.Key(15)] public int MaximumPageSize { get; init; } = 256;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    [MessagePack.Key(16)] public HistoricalVolatilitySource[] HistoricalSources { get; init; } =
        [HistoricalVolatilitySource.DatabentoQuoteHistory, HistoricalVolatilitySource.IfmReferenceMetadata];
    [MessagePack.Key(17)] public TradeIvBackfillPolicy TradeIvBackfill { get; init; } = TradeIvBackfillPolicy.Disallowed;
    [MessagePack.Key(18)] public bool AllowUnqualifiedBackfillPublication { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    [MessagePack.Key(19)] public OptionVolatilityEnvironment[] IsolatedEnvironments { get; init; } =
        [OptionVolatilityEnvironment.Production, OptionVolatilityEnvironment.Paper, OptionVolatilityEnvironment.Simulation];
}
