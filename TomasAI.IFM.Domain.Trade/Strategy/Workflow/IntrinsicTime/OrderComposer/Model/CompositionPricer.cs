using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Qualified deterministic calculation adapter over frozen inputs; never reads a feed or repository.</summary>
public interface IFuturesOptionComposerPricer
{
    string Version { get; }
    CompositionValuation Calculate(CompositionMarketInstrument instrument, DateTimeOffset at);
    decimal Tick(CompositionMarketInstrument instrument, decimal premium, bool allocatedLeg);
}

public sealed class Black76ComposerPricer : IFuturesOptionComposerPricer
{
    public string Version => OptionCalculator.EngineVersion + "/Decimal12-ToEven-v1";
    public CompositionValuation Calculate(CompositionMarketInstrument instrument, DateTimeOffset at)
    {
        if (instrument.Pricing is null || instrument.Underlying is null || instrument.Strike is null || instrument.IsCall is null)
            throw new CompositionException("OC.PRICING.REFERENCE_UNAVAILABLE");
        var priced = Application.MarketData.Pricing.Black76PricingModel.Calculate(
            CompositionSnapshotAdapter.To(instrument.Pricing), CompositionSnapshotAdapter.To(instrument.Underlying),
            CompositionSnapshotAdapter.To(instrument.Quote), instrument.Strike.Value, instrument.IsCall.Value, at);
        if (priced.Failure is { } failure) throw new CompositionException("OC.PRICING." + failure.Code);
        var p = priced.Value ?? throw new CompositionException("OC.PRICING.GREEKS_CALCULATION_FAILED");
        return new() { ImpliedVolatility = Normalize(p.ImpliedVolatility), Delta = Normalize(p.Delta),
            Gamma = Normalize(p.Gamma), Theta = Normalize(p.Theta), Vega = Normalize(p.Vega), Rho = Normalize(p.Rho),
            TheoreticalPrice = Normalize(p.TheoreticalPrice), TimeToExpiry = Normalize(p.TimeToExpiry), ContextDigest = p.ContextDigest };
    }
    public decimal Tick(CompositionMarketInstrument instrument, decimal premium, bool allocatedLeg) =>
        instrument.Pricing is { } p ? OptionPremiumTicks.GetIncrement(CompositionSnapshotAdapter.To(p.Contract), premium, allocatedLeg)
            : instrument.FutureDefinition?.TickSize ?? throw new CompositionException("OC.CONTRACT.REQUIRED_FIELD");
    static decimal Normalize(double value)
    {
        if (!double.IsFinite(value) || value > (double)decimal.MaxValue || value < (double)decimal.MinValue)
            throw new CompositionException("OC.PRICING.GREEKS_CALCULATION_FAILED");
        return decimal.Round((decimal)value, 12, MidpointRounding.ToEven);
    }
}
