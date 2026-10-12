using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

[MessagePackObject]
public sealed record CompositionBinding
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public SelectionCandidateIntent Selected { get; init; } = new();
    [Key(2)] public SelectionCatalogDefinitionSnapshot RulesDefinition { get; init; } = new();
    [Key(3)] public SelectionCatalogDefinitionSnapshot RulesSchema { get; init; } = new();
    [Key(4)] public OrderCompositionRules Rules { get; init; } = new();
    [Key(5)] public string BuilderCode { get; init; } = "";
    [Key(6)] public int BuilderVersion { get; init; }
    [Key(7)] public DateTime FrozenAtUtc { get; init; }
    [Key(8)] public DateTime ValidUntilUtc { get; init; }
    [Key(9)] public string BindingSha256 { get; init; } = "";
}
