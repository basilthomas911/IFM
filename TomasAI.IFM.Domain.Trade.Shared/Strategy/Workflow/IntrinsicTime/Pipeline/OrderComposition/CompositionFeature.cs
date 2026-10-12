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
public enum CompositionFeature
{
    Undefined = 0, RegimeConfidence = 1, SelectionConfidence = 2, ForwardPrice = 3, ImpliedVolatility = 4,
    /// <summary>0-100 percentage points from the exact accepted Stage 4 snapshot.</summary>
    IvRank = 5,
    /// <summary>0-100 percentage points from the exact accepted Stage 4 snapshot.</summary>
    IvPercentile = 6
}
