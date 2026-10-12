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
public sealed record CompositionAdjustment
{
    [JsonRequired, Key(0)] public string Code { get; init; } = "";
    [JsonRequired, Key(1)] public int Priority { get; init; }
    [JsonRequired, Key(2)] public CompositionPredicate Predicate { get; init; } = new();
    [JsonRequired, Key(3)] public CompositionParameter Parameter { get; init; }
    [JsonRequired, Key(4)] public CompositionOperation Operation { get; init; }
    [JsonRequired, Key(5)] public decimal Operand { get; init; }
}
