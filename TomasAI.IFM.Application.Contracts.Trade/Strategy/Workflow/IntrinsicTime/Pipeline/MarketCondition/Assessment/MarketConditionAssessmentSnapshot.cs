using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record MarketConditionAssessmentSnapshot
{
    AssessmentObservation[] _observations = [];
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid SnapshotId { get; init; }
    [Key(2)] public string MarketProfileId { get; init; } = string.Empty;
    [Key(3)] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(4)] public TimeFrameType TargetHorizon { get; init; }
    [Key(5)] public string ReferenceInstrumentId { get; init; } = string.Empty;
    [Key(6)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(7)] public AssessmentReferenceQuote? Quote { get; init; }
    [Key(8)] public MarketSessionStatus SessionState { get; init; }
    [Key(9)] public AssessmentEventContext EventContext { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(10)] public AssessmentObservation[] Observations { get => [.. _observations]; init => _observations = value is null ? [] : [.. value.OrderBy(x => x.SourceId, StringComparer.Ordinal)]; }
    [Key(11)] public MarketConditionCalendarDownloadEvidence? CalendarEvidence { get; init; }
    [Key(12)] public string PayloadSha256 { get; init; } = string.Empty;
    public string ComputeHash() => MarketConditionAssessmentHash.Compute(this with { PayloadSha256 = string.Empty, Observations = Observations });
    public MarketConditionAssessmentSnapshot Seal() => this with { PayloadSha256 = ComputeHash() };
}
