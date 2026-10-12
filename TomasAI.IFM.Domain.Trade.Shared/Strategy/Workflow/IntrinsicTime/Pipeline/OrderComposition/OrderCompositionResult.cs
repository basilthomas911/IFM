using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

[MessagePackObject]
public sealed record OrderCompositionResult
{
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public Guid InvocationId { get; init; }
    [Key(5)] public long InputWorkflowRevision { get; init; }
    [Key(6)] public string InputSha256 { get; init; } = "";
    [Key(7)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(8)] public DateTime ProducedAtUtc { get; init; }
    [Key(9)] public TimeFrameType TargetHorizon { get; init; }
    [Key(10)] public CompositionOutcome Outcome { get; init; }
    [Key(11)] public CompositionCandidate? Candidate { get; init; }
    [Key(12)] public CompositionDecisionContext DecisionContext { get; init; } = new();
    [Key(13)] public CompositionResolvedParameters ResolvedParameters { get; init; } = new();
    [Key(14)] public CompositionCounts CandidateCounts { get; init; } = new();
    [Key(15)] public ImmutableArray<CompositionRejection> CandidateDiagnostics { get; init; } = [];
    [Key(16)] public ImmutableArray<string> Reasons { get; init; } = [];
    [Key(17)] public DateTime? ValidUntilUtc { get; init; }
    [Key(18)] public string SummaryText { get; init; } = "";
    [Key(19)] public CompositionRanking? Ranking { get; init; }
}
