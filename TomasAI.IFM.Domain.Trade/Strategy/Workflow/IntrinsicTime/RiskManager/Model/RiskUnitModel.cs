using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>One frozen contract in the independently verified Risk calculation, in index points and USD.</summary>
public sealed record RiskLegInput(string InstrumentId, string UnderlyingId, bool IsFuture, bool IsCall,
    decimal Strike, int SignedRatio, decimal Multiplier, decimal Forward, decimal AnnualRate,
    decimal Volatility, decimal Years, decimal Delta, decimal Gamma, decimal Vega, decimal Theta);

/// <summary>Unit loss already includes the composer's fee/slippage reserve exactly once.</summary>


public sealed class RiskCalculationException(string reason) : ArgumentException(reason)
{
    public string ReasonCode { get; } = reason;
}

/// <summary>Pure payoff, Black-76 scenario and unit normalization calculations; no actor or storage access.</summary>
public static class RiskUnitModel
{
    static readonly decimal[] ForwardShocks = [-.20m, -.10m, -.05m, .05m, .10m, .20m];
    static readonly decimal[] VolatilityShocks = [-.10m, 0m, .10m];
    static readonly decimal[] ElapsedYears = [0m, 1m / 365m];

    /// <summary>Adapts only the candidate's exact contracts from its original immutable market snapshot.</summary>
    public static ImmutableArray<RiskLegInput> ReadLegs(CompositionCandidate candidate,
        MarketCompositionSnapshot snapshot, DateTime evaluatedAtUtc, string environment = "Production")
    {
        Require(candidate.CandidateHash == CompositionHash.Candidate(candidate)
            && candidate.SnapshotHash == snapshot.Digest
            && snapshot.Digest == CompositionSemanticHash.Compute(snapshot with { Digest = "" }), "RM.INPUT.HASH");
        Require(candidate.UnitQuantity == 1 && candidate.ApprovalState == "Unapproved"
            && candidate.Legs.Length is 1 or 2 or 4 && snapshot.Instruments.Length <= 10000, "RM.INPUT.CANDIDATE");
        Require(evaluatedAtUtc.Kind == DateTimeKind.Utc && candidate.EvaluatedAtUtc <= evaluatedAtUtc
            && candidate.ValidUntilUtc > evaluatedAtUtc && snapshot.ValidUntilUtc.UtcDateTime > evaluatedAtUtc
            && RiskLatency.WithinAgeLimit(environment, evaluatedAtUtc, candidate.EvaluatedAtUtc), "RM.INPUT.STALE");
        Require(candidate.ExecutionEnvelope.OrderType == "Limit" && candidate.ExecutionEnvelope.TimeInForce == "Day"
            && !candidate.ExecutionEnvelope.AllowLegging && !candidate.ExecutionEnvelope.AllowMarketEscalation
            && candidate.ExecutionEnvelope.WorstSignedDebit == candidate.Pricing.WorstDebit
            && candidate.ExecutionEnvelope.ValidUntilUtc >= candidate.ValidUntilUtc, "RM.INPUT.EXECUTION_ENVELOPE");
        Require(candidate.Greeks.DeltaUnits == "UnderlyingEquivalent"
            && candidate.Greeks.VegaUnits == "PerAnnualDecimalVolatility" && candidate.Greeks.ThetaUnits == "PerYear", "RM.INPUT.GREEK_UNITS");
        Require(snapshot.Instruments.Select(x => x.Instrument.ContractId).Distinct(StringComparer.Ordinal).Count()
            == snapshot.Instruments.Length, "RM.INPUT.DUPLICATE_CONTRACT");
        var instruments = snapshot.Instruments.ToDictionary(x => x.Instrument.ContractId, StringComparer.Ordinal);
        var output = ImmutableArray.CreateBuilder<RiskLegInput>(candidate.Legs.Length);
        foreach (var leg in candidate.Legs)
        {
            Require(instruments.TryGetValue(leg.InstrumentId, out var frozen), "RM.INPUT.CONTRACT_MISSING");
            var instrument = frozen!.Instrument;
            Require(leg.Quote == instrument.Quote && leg.Side is "Buy" or "Sell" && leg.Ratio is > 0 and <= 100,
                "RM.INPUT.LEG");
            Require(instrument.Quote.GenerationId == snapshot.GenerationId, "RM.INPUT.GENERATION");
            ValidateQuote(instrument.Quote, evaluatedAtUtc, environment);
            int signedRatio = leg.Side == "Buy" ? leg.Ratio : -leg.Ratio;
            if (leg.InstrumentClass == "Futures")
            {
                var definition = instrument.FutureDefinition;
                Require(candidate.Legs.Length == 1 && definition is not null && definition.Root == "ES"
                    && definition.Currency == "USD" && definition.ContractId == leg.InstrumentId
                    && definition.Multiplier == leg.Multiplier && definition.DefinitionDigest == leg.DefinitionHash
                    && definition.LastTradingUtc.UtcDateTime > evaluatedAtUtc && leg.Valuation is null,
                    "RM.INPUT.FUTURE_DEFINITION");
                output.Add(new(leg.InstrumentId, leg.InstrumentId, true, false, 0, signedRatio,
                    leg.Multiplier, Mid(instrument.Quote), 0, 0, 0, 1, 0, 0, 0));
                continue;
            }
            Require(leg.InstrumentClass == "FuturesOption" && candidate.ExecutionEnvelope.Atomic
                && instrument.Pricing is not null && instrument.Underlying is not null
                && leg.Valuation is not null && instrument.Strike == leg.Strike && instrument.IsCall == leg.Right,
                "RM.INPUT.OPTION");
            var pricing = instrument.Pricing!;
            var definitionOption = pricing.Contract;
            Require(definitionOption.Root == "ES" && definitionOption.Currency == "USD"
                && definitionOption.ExerciseStyle == OptionExerciseStyle.European
                && definitionOption.DayCount == PricingDayCount.Actual365Fixed
                && definitionOption.SettlementStyle is OptionSettlementStyle.Cash or OptionSettlementStyle.DeliveryOfFuture
                && definitionOption.ContractId == leg.InstrumentId && definitionOption.UnderlyingContractId == leg.UnderlyingInstrumentId
                && definitionOption.Multiplier == leg.Multiplier && definitionOption.DefinitionDigest == leg.DefinitionHash
                && definitionOption.ExpirationUtc.UtcDateTime == leg.ExpirationUtc
                && definitionOption.LastTradingUtc.UtcDateTime > evaluatedAtUtc
                && definitionOption.EffectiveFromUtc.UtcDateTime <= evaluatedAtUtc
                && definitionOption.EffectiveUntilUtc.UtcDateTime > evaluatedAtUtc
                && pricing.ValidUntilUtc.UtcDateTime > evaluatedAtUtc
                && pricing.GenerationId == snapshot.GenerationId && instrument.Underlying!.GenerationId == snapshot.GenerationId,
                "RM.INPUT.OPTION_DEFINITION");
            ValidateQuote(instrument.Underlying!, evaluatedAtUtc, environment);
            Require(Math.Abs((instrument.Quote.EventAtUtc - instrument.Underlying!.EventAtUtc).TotalMilliseconds) <= 250,
                "RM.INPUT.QUOTE_SKEW");
            // Recalculate from the frozen quotes and conventions; a self-consistent candidate hash is not pricing evidence.
            var value = new Black76ComposerPricer().Calculate(instrument, new DateTimeOffset(candidate.EvaluatedAtUtc));
            Require(value == leg.Valuation, "RM.INPUT.VALUATION_MISMATCH");
            Require(double.IsFinite(pricing.Rate.AnnualContinuousRate)
                && pricing.Rate.AnnualContinuousRate is >= -1 and <= 1
                && value.ContextDigest.Length == 64 && value.ImpliedVolatility is > 0 and <= 10,
                "RM.INPUT.VALUATION");
            var years = (decimal)(leg.ExpirationUtc - evaluatedAtUtc).TotalSeconds / (365m * 86400m);
            output.Add(new(leg.InstrumentId, leg.UnderlyingInstrumentId, false, leg.Right!.Value,
                leg.Strike!.Value, signedRatio, leg.Multiplier, Mid(instrument.Underlying!),
                (decimal)pricing.Rate.AnnualContinuousRate, value.ImpliedVolatility, years,
                value.Delta, value.Gamma, value.Vega, value.Theta));
        }
        return output.MoveToImmutable();
    }

    /// <summary>Checks all payoff breakpoints and both outer slopes, then evaluates the pinned 36-scenario grid.</summary>
    public static RiskUnitResult Calculate(ImmutableArray<RiskLegInput> legs, decimal worstDebit,
        decimal composerCostReserve, decimal incrementalLossReserve, decimal? plannedFutureLoss,
        decimal? composerFutureStressLoss, CancellationToken cancellationToken = default)
    {
        Require(legs.Length is 1 or 2 or 4 && composerCostReserve >= 0 && incrementalLossReserve >= 0,
            "RM.CALCULATION.INPUT");
        Require(legs.Select(x => x.InstrumentId).Distinct(StringComparer.Ordinal).Count() == legs.Length
            && legs.Select(x => x.UnderlyingId).Distinct(StringComparer.Ordinal).Count() == 1
            && legs.Select(x => x.Multiplier).Distinct().Count() == 1
            && legs.All(x => x.Multiplier is > 0 and <= 1000000 && x.Forward is > 0 and <= 100000000
                && x.SignedRatio is >= -100 and <= 100 && x.SignedRatio != 0), "RM.CALCULATION.CONTRACT");
        bool future = legs[0].IsFuture;
        Require(future ? legs.Length == 1 : legs.All(x => !x.IsFuture && x.Strike > 0 && x.Years > 0
            && x.Volatility is > 0 and <= 10 && x.AnnualRate is >= -1 and <= 1), "RM.CALCULATION.UNIVERSE");
        Require(legs.All(x => x.Forward == legs[0].Forward)
            && (future || legs.All(x => x.Years == legs[0].Years && x.AnnualRate == legs[0].AnnualRate)),
            "RM.CALCULATION.MIXED_REFERENCE");
        decimal multiplier = legs[0].Multiplier;
        decimal? maxLoss = null;
        if (!future)
        {
            Require(legs.Where(x => x.IsCall).Sum(x => x.SignedRatio) == 0
                && legs.Where(x => !x.IsCall).Sum(x => x.SignedRatio) == 0, "RM.CALCULATION.UNBOUNDED_OPTIONS");
            decimal minimumPayoff = legs.Select(x => x.Strike).Append(0m).Distinct().Min(price =>
                legs.Sum(x => x.SignedRatio * Intrinsic(price, x.Strike, x.IsCall)));
            maxLoss = Math.Max(0, (worstDebit - minimumPayoff) * multiplier) + composerCostReserve;
        }
        else Require(plannedFutureLoss is > 0 && composerFutureStressLoss is > 0,
            "RM.CALCULATION.FUTURES_LOSS_MISSING");
        decimal scenarioLoss = 0;
        int count = 0;
        foreach (var forwardShock in ForwardShocks)
        foreach (var volatilityShock in VolatilityShocks)
        foreach (var elapsed in ElapsedYears)
        {
            cancellationToken.ThrowIfCancellationRequested();
            decimal value = 0;
            foreach (var leg in legs)
            {
                decimal forward = leg.Forward * (1 + forwardShock);
                decimal price = future ? forward : Normalize(OptionModel.Price((double)forward, (double)leg.Strike,
                    (double)leg.AnnualRate, (double)Math.Max(.0001m, leg.Volatility + volatilityShock),
                    (double)Math.Max(0, leg.Years - elapsed), leg.IsCall ? 1 : -1));
                value += leg.SignedRatio * price;
            }
            scenarioLoss = Math.Max(scenarioLoss, (worstDebit - value) * multiplier + composerCostReserve);
            count++;
        }
        decimal loss = Math.Max(maxLoss ?? Math.Max(plannedFutureLoss!.Value, composerFutureStressLoss!.Value), scenarioLoss)
            + incrementalLossReserve;
        return new(maxLoss is null ? null : CeilingMoney(maxLoss.Value), CeilingMoney(scenarioLoss), CeilingMoney(loss),
            future ? 0 : CeilingMoney(Math.Max(0, worstDebit * multiplier)),
            CeilingMoney(legs.Sum(x => Math.Abs(x.SignedRatio) * x.Forward * x.Multiplier)),
            legs.Sum(x => Math.Abs(x.SignedRatio)),
            legs.Sum(x => x.SignedRatio * x.Delta * x.Multiplier),
            legs.Sum(x => x.SignedRatio * x.Gamma * x.Multiplier),
            legs.Sum(x => x.SignedRatio * x.Vega * x.Multiplier) * .01m,
            legs.Sum(x => x.SignedRatio * x.Theta * x.Multiplier) / 365m, count);
    }

    static void ValidateQuote(OptionPricingQuote quote, DateTime at, string environment) => Require(quote.Bid >= 0 && quote.Ask >= quote.Bid
        && quote.BidSize > 0 && quote.AskSize > 0 && quote.EventAtUtc.UtcDateTime <= at
        && quote.ReceivedAtUtc >= quote.EventAtUtc && quote.ReceivedAtUtc.UtcDateTime <= at
        && RiskLatency.WithinAgeLimit(environment, at, quote.EventAtUtc.UtcDateTime), "RM.INPUT.QUOTE");
    static decimal Mid(OptionPricingQuote quote) => (quote.Bid + quote.Ask) / 2m;
    static decimal Intrinsic(decimal price, decimal strike, bool call) => Math.Max(0, call ? price - strike : strike - price);
    static decimal Normalize(double value)
    {
        // Deep OTM puts computed by parity can carry a few binary rounding ulps below zero.
        // Normalize only sub-1e-10 index-point noise; meaningful negative/non-finite prices still fail.
        Require(double.IsFinite(value) && value >= -1e-10 && value <= (double)decimal.MaxValue, "RM.CALCULATION.NONFINITE");
        return decimal.Round((decimal)Math.Max(0, value), 12, MidpointRounding.ToEven);
    }
    internal static decimal CeilingMoney(decimal value) => decimal.Ceiling(value * 100m) / 100m;
    internal static void Require(bool condition, string reason)
    {
        if (!condition) throw new RiskCalculationException(reason);
    }
}
