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
public sealed record SelectionLeg
{
    [Key(0)] public string Key { get; init; } = string.Empty;
    [Key(1)] public string InstrumentClass { get; init; } = string.Empty;
    [Key(2)] public string Side { get; init; } = string.Empty;
    [Key(3)] public string OptionRight { get; init; } = string.Empty;
    [Key(4)] public decimal Ratio { get; init; }
    [Key(5)] public string ExpiryGroup { get; init; } = string.Empty;
}
