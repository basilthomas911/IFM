using MessagePack;
using System.Collections.Immutable;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

[MessagePackObject]
public sealed record OptionPricingValue(
    [property: Key(0)] double ImpliedVolatility,
    [property: Key(1)] double Delta,
    [property: Key(2)] double Gamma,
    [property: Key(3)] double Theta,
    [property: Key(4)] double Vega,
    [property: Key(5)] double Rho,
    [property: Key(6)] double TheoreticalPrice,
    [property: Key(7)] double TimeToExpiry,
    [property: Key(8)] string ContextDigest);
