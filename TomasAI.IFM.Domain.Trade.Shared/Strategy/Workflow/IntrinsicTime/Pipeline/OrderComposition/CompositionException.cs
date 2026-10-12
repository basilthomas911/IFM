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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified pipeline exception requires an explicit reason code; unclassified constructors are intentionally unavailable.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified pipeline exception requires an explicit reason code; unclassified constructors are intentionally unavailable.")]
public sealed class CompositionException(string code) : ArgumentException(code)
{
    public string ReasonCode { get; } = code;
}
