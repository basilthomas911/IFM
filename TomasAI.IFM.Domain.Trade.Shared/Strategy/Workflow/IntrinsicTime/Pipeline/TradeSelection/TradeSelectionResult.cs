using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

[MessagePackObject]
public sealed record TradeSelectionResult
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public Guid InvocationId { get; init; }
    [Key(5)] public long InputWorkflowRevision { get; init; }
    [Key(6)] public Guid TriggerEventId { get; init; }
    [Key(7)] public int PortfolioId { get; init; }
    [Key(8)] public int FundId { get; init; }
    [Key(9)] public TimeFrameType DecisionHorizon { get; init; }
    [Key(10)] public SelectionOutcome Outcome { get; init; }
    [Key(11)] public SelectionCandidateIntent? SelectedCandidate { get; init; }
    [Key(12)] public TradeSelectionDecisionContext DecisionContext { get; init; }
    SelectionRuleEvidence[] _GlobalEvidence = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(13)] public SelectionRuleEvidence[] GlobalEvidence { get => [.. _GlobalEvidence]; init => _GlobalEvidence = value is null ? [] : [.. value]; }
    SelectionCandidateDecision[] _CandidateDecisions = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(14)] public SelectionCandidateDecision[] CandidateDecisions { get => [.. _CandidateDecisions]; init => _CandidateDecisions = value is null ? [] : [.. value]; }
    [Key(15)] public decimal SelectionConfidence { get; init; }
    [Key(16)] public decimal? CompatibilityScore { get; init; }
    [Key(17)] public string PrimaryReasonCode { get; init; } = string.Empty;
    [Key(18)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(19)] public DateTime ProducedAtUtc { get; init; }
    [Key(20)] public DateTime ValidUntilUtc { get; init; }
    [Key(21)] public SelectionPipelinePolicyReference CommonPolicyReference { get; init; }
    [Key(22)] public string SummaryText { get; init; } = string.Empty;
    [Key(23)] public VolatilityWorkflowEvidence? AcceptedVolatilityEvidence { get; init; }
    /// <summary>Copies owned collections; nested records expose only init setters and defensive collection accessors.</summary>
    public TradeSelectionResult CopyContent() => this with
    {
        GlobalEvidence = GlobalEvidence,
        CandidateDecisions = CandidateDecisions,
        DecisionContext = DecisionContext with { SelectionBinding = DecisionContext.SelectionBinding with { } },
        SelectedCandidate = SelectedCandidate is null ? null : SelectedCandidate with { }
    };

    /// <summary>Fingerprints semantic content while measuring the established uncompressed MessagePack budget.</summary>
    public (string Hash, int Size) ContentFingerprint() =>
        (MarketCondition.Assessment.MarketConditionAssessmentHash.Compute(this),
         TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.MeasureContent(this));

}
