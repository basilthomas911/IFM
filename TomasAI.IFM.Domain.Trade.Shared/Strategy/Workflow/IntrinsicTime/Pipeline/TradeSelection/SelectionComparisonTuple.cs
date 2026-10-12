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
public sealed record SelectionComparisonTuple
{
    [Key(0)] public int Priority { get; init; }
    [Key(1)] public int Preference { get; init; }
    [Key(2)] public CatalogKey Deployment { get; init; }
    [Key(3)] public CatalogKey Strategy { get; init; }
    [Key(4)] public CatalogKey Structure { get; init; }
    [Key(5)] public CatalogKey Variant { get; init; }
    [Key(6)] public int ProductId { get; init; }
    [Key(7)] public long AssignmentVersion { get; init; }
}
