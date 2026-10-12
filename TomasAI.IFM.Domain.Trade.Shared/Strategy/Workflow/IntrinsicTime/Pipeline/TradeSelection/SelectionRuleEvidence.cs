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
public sealed record SelectionRuleEvidence
{
    [Key(0)] public string RuleId { get; init; } = string.Empty;
    [Key(1)] public string FieldPath { get; init; } = string.Empty;
    [Key(2)] public SelectionRuleStatus Status { get; init; }
    [Key(3)] public string ActualJson { get; init; } = string.Empty;
    [Key(4)] public string ExpectedJson { get; init; } = string.Empty;
    [Key(5)] public string ReasonCode { get; init; } = string.Empty;
}
