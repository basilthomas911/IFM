using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Prices the four established option legs with the unified calculator, without provider access or state mutation.</summary>
public static class IronCondorOptionCalculator
{
    /// <summary>Calculates signed put/call and combined spread prices from exact contract conventions and qualified IV evidence.</summary>
    /// <param name="legs">The four persisted established legs, with equal whole strategy quantities.</param>
    /// <param name="risk">One qualified pricing scope for each exact option contract.</param>
    /// <param name="atUtc">A single captured UTC valuation instant; no machine clock is read during calculation.</param>
    /// <param name="token">The background refresh deadline, observed by each calculator call.</param>
    /// <returns>Current per-strategy theoretical spread prices; no forward scenario or loss distribution is fabricated.</returns>
    /// <exception cref="ArgumentException">Required option evidence, identity, conventions or freshness is invalid.</exception>
    /// <exception cref="InvalidOperationException">One leg cannot be valued, so no partial strategy price is returned.</exception>
    /// <exception cref="OperationCanceledException">The calculation deadline expires.</exception>
    public static IronCondorCalculatedSpreadPrices Calculate(TradeLegDefinition[] legs,
        MarketCompositionSnapshot[] risk, DateTime atUtc, CancellationToken token = default)
    {
        _ = IronCondorMonitoringInputReader.IdentifyTradeType(legs);
        if (atUtc.Kind != DateTimeKind.Utc || risk.Length != 4 || risk.Select(scope => scope.GenerationId).Distinct().Count() != 1
            || risk.Any(scope => scope.GenerationId == Guid.Empty || scope.ValidUntilUtc.UtcDateTime <= atUtc
                || scope.EvaluatedAtUtc.UtcDateTime > atUtc || scope.Instruments.Length != 1))
            throw new ArgumentException("Four current scopes in one admitted generation and a UTC valuation instant are required.");
        if (legs.Select(leg => leg.CashMultiplier).Distinct().Count() != 1 || legs.Any(leg => leg.CashMultiplier <= 0)
            || legs.Any(leg => leg.Expiry is null)
            || legs.Select(leg => leg.ContractId).Distinct(StringComparer.Ordinal).Count() != 4)
            throw new ArgumentException("Distinct exact contracts, each leg expiry and positive multiplier are required.");
        var calculator = new OptionCalculator();
        var prices = new IronCondorCalculatedOptionPrice[4];
        var at = new DateTimeOffset(atUtc);
        for (var index = 0; index < legs.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var leg = legs[index];
            var item = risk.SelectMany(scope => scope.Instruments).SingleOrDefault(item => item.Instrument.ContractId == leg.ContractId);
            if (item?.Valuation is not { } valuation || item.Instrument.Pricing is not { } context
                || item.Instrument.Quote is not { } quote || item.Instrument.Underlying is not { } underlying
                || leg.Strike is not { } strike || item.Instrument.Strike != strike || item.Instrument.IsCall != (leg.PutCall == 1)
                || !double.IsFinite(valuation.ImpliedVolatility) || valuation.ImpliedVolatility <= 0)
                throw new ArgumentException($"Qualified pricing evidence for option {leg.ContractId} is unavailable.");
            var invalid = Black76PricingModel.ValidateInputs(context, underlying, quote, strike, leg.PutCall == 1, at);
            if (invalid is not null) throw new ArgumentException($"{leg.ContractId}: {invalid.Code};{invalid.Input}");
            if (context.GenerationId != risk[0].GenerationId || context.Contract.Multiplier != leg.CashMultiplier
                || context.Contract.ContractId != leg.ContractId)
                throw new ArgumentException($"{leg.ContractId}: pricing scope generation, identity or cash multiplier differs from the established trade.");
            var years = OptionPricingQualification.YearFraction(context.Contract, at);
            var future = (double)(underlying.Bid / 2m + underlying.Ask / 2m);
            var request = Black76PricingModel.CreateRequest(context.Contract, future, strike, leg.PutCall == 1,
                years, context.Rate.AnnualContinuousRate);
            var result = calculator.TheoreticalPrice(request, valuation.ImpliedVolatility, token);
            if (!result.Success) throw new InvalidOperationException($"{leg.ContractId}: OptionCalculator failed; {result.Failure}.");
            prices[index] = new()
            {
                ContractId = leg.ContractId, SignedRatio = Math.Sign(leg.SignedQuantity), IsCall = leg.PutCall == 1,
                Strike = strike, UnderlyingPrice = future, ImpliedVolatility = valuation.ImpliedVolatility, TimeToExpiry = years,
                AnnualContinuousRate = request.Rate, ExerciseStyle = request.Exercise.ToString(), PremiumStyle = request.Premium.ToString(),
                TheoreticalPrice = result.Price!.Value, EngineVersion = result.EngineVersion, ContextDigest = valuation.ContextDigest,
                QuoteAsOfUtc = quote.EventAtUtc.UtcDateTime, UnderlyingAsOfUtc = underlying.EventAtUtc.UtcDateTime,
                MappingVersion = context.Contract.MappingVersion, GenerationId = context.GenerationId
            };
        }
        var contracts = risk.Select(scope => scope.Instruments[0].Instrument.Pricing!.Contract).ToArray();
        if (contracts.Select(contract => contract.UnderlyingContractId).Distinct(StringComparer.Ordinal).Count() != 1
            || contracts.Select(contract => contract.Currency).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new ArgumentException("The four qualified option contracts must share one underlying and currency.");
        var timestamps = prices.SelectMany(leg => new[] { leg.QuoteAsOfUtc, leg.UnderlyingAsOfUtc }).ToArray();
        if (timestamps.Max() - timestamps.Min() > TimeSpan.FromSeconds(1))
            throw new ArgumentException("Four-leg pricing source timestamps are incoherent.");
        var put = prices.Where(leg => !leg.IsCall).Sum(leg => leg.SignedRatio * leg.TheoreticalPrice);
        var call = prices.Where(leg => leg.IsCall).Sum(leg => leg.SignedRatio * leg.TheoreticalPrice);
        if (!double.IsFinite(put) || !double.IsFinite(call) || !double.IsFinite(put + call)
            || !double.IsFinite(Math.Abs(put) + Math.Abs(call)))
            throw new InvalidOperationException("IronCondorPricing.SPREAD.NONFINITE: complete spread pricing is unavailable.");
        return new()
        {
            PutSpreadPrice = put, CallSpreadPrice = call, SignedSpreadPrice = put + call,
            CombinedSpreadPrice = Math.Abs(put) + Math.Abs(call), CalculatedAtUtc = atUtc,
            ValidUntilUtc = risk.Min(scope => scope.ValidUntilUtc).UtcDateTime, OptionLegPrices = prices,
            CalculatorVersion = OptionCalculator.Version, NumericalPolicyVersion = PricingSettings.Version
        };
    }
}
