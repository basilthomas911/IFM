using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record MarketConditionAssessmentResult
{
    /// <summary>Copies owned collections without a serialization round trip.</summary>
    public MarketConditionAssessmentResult CopyContent() => this with
    {
        Assessment = Assessment is null ? null! : Assessment with
        {
            UpstreamContext = RegimeDiscoveryResultContent.Clone(Assessment.UpstreamContext),
            EvidenceItems = Assessment.EvidenceItems,
            ConflictingEvidenceItems = Assessment.ConflictingEvidenceItems,
            LimitationReasons = Assessment.LimitationReasons,
            InheritedRestrictions = Assessment.InheritedRestrictions
        },
        CalendarEvidence = CalendarEvidence is null ? null : CalendarEvidence with { Attempts = CalendarEvidence.Attempts }
    };

    /// <summary>Fingerprints canonical typed content using the same numeric normalization as assessment snapshots.</summary>
    /// <remarks>The canonical JSON is used only for hashing and size validation; it is not embedded in the event.</remarks>
    public (string Hash, int Size) ContentFingerprint()
    {
        var content = System.Text.Encoding.UTF8.GetBytes(MarketConditionAssessmentHash.Serialize(this));
        return (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), content.Length);
    }

    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public long InputWorkflowRevision { get; init; }
    [Key(6)] public string MarketProfileId { get; init; } = string.Empty;
    [Key(7)] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(8)] public Guid ParameterSetId { get; init; }
    [Key(9)] public int ParameterSetVersion { get; init; }
    [Key(10)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(11)] public Guid RegimeResultId { get; init; }
    [Key(12)] public string RegimePayloadSha256 { get; init; } = string.Empty;
    [Key(13)] public Guid SnapshotId { get; init; }
    [Key(14)] public string SnapshotSha256 { get; init; } = string.Empty;
    [Key(15)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(16)] public TimeFrameType TargetHorizon { get; init; }
    [Key(17)] public HorizonAssessment Assessment { get; init; } = new();
    [Key(18)] public string SummaryText { get; init; } = string.Empty;
    [Key(19)] public MarketConditionCalendarDownloadEvidence? CalendarEvidence { get; init; }
}
