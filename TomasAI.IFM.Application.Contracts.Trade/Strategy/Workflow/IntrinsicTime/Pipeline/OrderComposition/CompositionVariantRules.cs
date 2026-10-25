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
public sealed record CompositionVariantRules
{
    [JsonRequired, Key(0)] public CatalogKey VariantKey { get; init; } = default!;
    [JsonRequired, Key(1)] public CatalogKey StructureKey { get; init; } = default!;
    [JsonRequired, Key(2)] public CompositionParameters BaseParameters { get; init; } = new();
    [JsonRequired, Key(3)] public ImmutableArray<CompositionParameterBound> HardBounds { get; init; } = [];
    [JsonRequired, Key(4)] public ImmutableArray<CompositionAdjustment> AdjustmentRules { get; init; } = [];
    [JsonRequired, Key(5)] public ImmutableArray<decimal> AllowedWidths { get; init; } = [];
    [JsonRequired, Key(6)] public bool RequireSymmetricWings { get; init; }
    [JsonRequired, Key(7)] public string DeltaUnits { get; init; } = "";
    [JsonRequired, Key(8)] public string RankingVersion { get; init; } = "";
}
