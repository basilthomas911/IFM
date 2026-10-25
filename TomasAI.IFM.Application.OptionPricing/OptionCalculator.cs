using TomasAI.IFM.Shared.OptionPricing;
namespace TomasAI.IFM.Application.OptionPricing;
/// <summary>Application pricing boundary; the qualified numerical engine remains in Framework.</summary>
public sealed class OptionCalculator
{
    // The engine has immutable settings and no per-request mutable state. Reuse avoids adding a provider allocation on each quote.
    static readonly TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator Default = new();
    readonly TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator inner;
    public const string Version = TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator.Version;
    public OptionCalculator(PricingSettings? settings = null) => inner = settings is null ? Default : new(settings);
    public static string EngineVersionFor(in OptionPricingRequest request) => TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator.EngineVersionFor(request);
    public OptionPriceResult TheoreticalPrice(in OptionPricingRequest request, double volatility, CancellationToken cancellationToken = default) => inner.TheoreticalPrice(request, volatility, cancellationToken);
    public PriceDeltaResult PriceAndDelta(in OptionPricingRequest request, double volatility, CancellationToken cancellationToken = default) => inner.PriceAndDelta(request, volatility, cancellationToken);
    public PricingResult Price(in OptionPricingRequest request, double volatility, CancellationToken cancellationToken = default) => inner.Price(request, volatility, cancellationToken);
    public PricingResult ImpliedVolatility(in OptionPricingRequest request, double price, CancellationToken cancellationToken = default) => inner.ImpliedVolatility(request, price, cancellationToken);
    public ImpliedVolatilityResult SolveImpliedVolatility(in OptionPricingRequest request, double price, CancellationToken cancellationToken = default) => inner.SolveImpliedVolatility(request, price, cancellationToken);
    public void PriceBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> volatilities, Span<PricingResult> results, CancellationToken cancellationToken = default) => inner.PriceBatch(requests, volatilities, results, cancellationToken);
    public void ImpliedVolatilityBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> prices, Span<PricingResult> results, CancellationToken cancellationToken = default) => inner.ImpliedVolatilityBatch(requests, prices, results, cancellationToken);
    public void PriceAndDeltaBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> volatilities, Span<PriceDeltaResult> results, CancellationToken cancellationToken = default) => inner.PriceAndDeltaBatch(requests, volatilities, results, cancellationToken);
    public void SolveImpliedVolatilityBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> prices, Span<ImpliedVolatilityResult> results, CancellationToken cancellationToken = default) => inner.SolveImpliedVolatilityBatch(requests, prices, results, cancellationToken);
}
