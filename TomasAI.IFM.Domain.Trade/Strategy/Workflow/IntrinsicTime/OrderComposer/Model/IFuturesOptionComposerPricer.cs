using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
/// <summary>Qualified deterministic calculation adapter over frozen inputs; never reads a feed or repository.</summary>
public interface IFuturesOptionComposerPricer
{
    string Version { get; }

    CompositionValuation Calculate(CompositionMarketInstrument instrument, DateTimeOffset at);
    decimal Tick(CompositionMarketInstrument instrument, decimal premium, bool allocatedLeg);
}
