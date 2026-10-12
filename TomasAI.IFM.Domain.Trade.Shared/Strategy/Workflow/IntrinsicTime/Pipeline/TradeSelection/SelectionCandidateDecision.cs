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
public sealed record SelectionCandidateDecision
{
    [Key(0)] public string CandidateHash { get; init; } = string.Empty;
    [Key(1)] public SelectionCandidateStatus Status { get; init; }
    SelectionRuleEvidence[] _RuleEvidence = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(2)] public SelectionRuleEvidence[] RuleEvidence { get => [.. _RuleEvidence]; init => _RuleEvidence = value is null ? [] : [.. value]; }
    [Key(3)] public SelectionComparisonTuple Comparison { get; init; }
    string[] _ReasonCodes = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(4)] public string[] ReasonCodes { get => [.. _ReasonCodes]; init => _ReasonCodes = value is null ? [] : [.. value]; }
}
