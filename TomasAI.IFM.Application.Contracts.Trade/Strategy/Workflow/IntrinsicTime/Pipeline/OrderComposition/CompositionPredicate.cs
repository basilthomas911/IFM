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
public sealed record CompositionPredicate
{
    [JsonRequired, Key(0)] public CompositionComparison Comparison { get; init; }
    [JsonRequired, Key(1)] public CompositionFeature Feature { get; init; }
    [JsonRequired, Key(2)] public ImmutableArray<decimal> Values { get; init; } = [];
    [JsonRequired, Key(3)] public ImmutableArray<CompositionPredicate> Children { get; init; } = [];
    [JsonRequired, Key(4)] public bool Required { get; init; }
}
