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
public sealed record OrderCompositionRules
{
    [JsonRequired, Key(0)] public short SchemaVersion { get; init; }
    [JsonRequired, Key(1)] public string AlgorithmVersion { get; init; } = "";
    [JsonRequired, Key(2)] public string PricerVersion { get; init; } = "";
    [JsonRequired, Key(3)] public TimeFrameType SupportedHorizon { get; init; }
    [JsonRequired, Key(4)] public string InstrumentRoot { get; init; } = "";
    [JsonRequired, Key(5)] public string Currency { get; init; } = "";
    [JsonRequired, Key(6)] public ImmutableArray<CompositionVariantRules> VariantRules { get; init; } = [];
}
