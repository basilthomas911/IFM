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
public sealed record SelectionPipelineParameter
{
    [Key(0)] public string Role { get; init; } = string.Empty;
    [Key(1)] public CatalogPipelineParameterKind Kind { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public int Version { get; init; }
    [Key(4)] public string Hash { get; init; } = string.Empty;
}
