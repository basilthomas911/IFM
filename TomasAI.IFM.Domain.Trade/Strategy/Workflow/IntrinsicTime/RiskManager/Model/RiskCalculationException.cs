using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Application.OptionPricing;
using TomasAI.IFM.Shared.OptionPricing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
/// <summary>Unit loss already includes the composer's fee/slippage reserve exactly once.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This domain failure requires structured reason or stream/revision evidence; generic constructors would create failures without the identity required by its consumer.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The domain exception requires structured reason or stream/revision evidence; generic constructors would omit facts required by its consumer.")]
public sealed class RiskCalculationException(string reason) : ArgumentException(reason)
{
    public string ReasonCode { get; } = reason;
}
