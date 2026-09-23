using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using Unified = TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Unscaled per-option values; composition applies signed ratios and multipliers exactly once.</summary>
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

[MessagePackObject]
public sealed record OptionPricingPassResult(
    [property: Key(0)] OptionPricingValue? Value,
    [property: Key(1)] OptionPricingFailure? Failure);

/// <summary>Compatibility entry point routing qualified futures options through the unified calculator.</summary>
public static partial class Black76PricingModel
{
    public static OptionPricingPassResult Calculate(OptionPricingContext context, OptionPricingQuote underlying,
        OptionPricingQuote option, decimal strike, bool isCall, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(option);
        var referenceFailure = ValidateInputs(context, underlying, option, strike, isCall, at);
        if (referenceFailure is not null) return new(null, referenceFailure);
        var contract = context.Contract;
        OptionPricingPassResult Fail(string code, string input) => new(null,
            new(code, input, contract.ContractId, "Option pricing inputs do not satisfy the qualified context."));
        var t = OptionPricingQualification.YearFraction(contract, at);
        var forward = (double)(underlying.Bid / 2m + underlying.Ask / 2m);
        var mark = (double)(option.Bid / 2m + option.Ask / 2m);
        var rate = context.Rate.AnnualContinuousRate;
        var request = CreateRequest(contract, forward, strike, isCall, t, rate);
        var result = new Unified.OptionCalculator().ImpliedVolatility(request, mark);
        if (!result.Success) return Fail("GreeksCalculationFailed", result.Failure.ToString());
        var greeks = result.Value!.Value;
        var digest = PricingSemanticHash.Compute(new { Version = 2, context, underlying, option, strike, isCall, at, t,
            result.EngineVersion, result.NumericalPolicy, result.PolicyVersion });
        return new(new(greeks.Volatility, greeks.Delta, greeks.Gamma, greeks.Theta,
            greeks.Vega, greeks.Rho, greeks.Price, t, digest), null);
    }

    public static OptionPricingFailure? ValidateInputs(OptionPricingContext context, OptionPricingQuote underlying,
        OptionPricingQuote option, decimal strike, bool isCall, DateTimeOffset at)
    {
        var invalid = ValidateContext(context, at);
        if (invalid is not null) return invalid;
        var contract = context.Contract;
        OptionPricingFailure Fail(string code, string input) =>
            new(code, input, contract.ContractId, "Option pricing inputs do not satisfy the qualified context.");
        if (underlying.GenerationId != context.GenerationId || option.GenerationId != context.GenerationId)
            return Fail("Recovering", "Generation");
        if (option.ContractId != contract.ContractId || underlying.ContractId != contract.UnderlyingContractId || strike <= 0
            || contract.SchemaVersion == 3 && (contract.Strike != strike
                || contract.Right != (isCall ? PricingOptionRight.Call : PricingOptionRight.Put)))
            return Fail("ContractMetadataUnavailable", "Identity/Strike");
        foreach (var quote in new[] { underlying, option })
        {
            if (quote.Bid <= 0 || quote.Ask < quote.Bid || quote.BidSize < 0 || quote.AskSize < 0 || quote.Sequence < 0
                || quote.EventAtUtc.Offset != TimeSpan.Zero || quote.ReceivedAtUtc.Offset != TimeSpan.Zero
                || quote.EventAtUtc > at.AddMilliseconds(context.MaximumSourceClockLeadMilliseconds)
                || quote.ReceivedAtUtc > at)
                return Fail("InvalidQuote", quote.ContractId);
            if ((at - quote.EventAtUtc).TotalMilliseconds > context.MaximumQuoteAgeMilliseconds
                || (at - quote.ReceivedAtUtc).TotalMilliseconds > context.MaximumQuoteAgeMilliseconds)
                return Fail("StaleData", quote.ContractId);
        }
        if (Math.Abs((underlying.EventAtUtc - option.EventAtUtc).TotalMilliseconds) > context.MaximumQuoteSkewMilliseconds)
            return Fail("IncoherentQuotes", "SourceTimeSkew");
        return null;
    }

    public static string EngineFor(OptionPricingConvention contract) =>
        Unified.OptionCalculator.EngineVersionFor(CreateRequest(contract, 1, 1, true, 1, 0));

    internal static Unified.OptionPricingRequest CreateRequest(OptionPricingConvention contract, double forward,
        decimal strike, bool isCall, double time, double rate) =>
        new(Unified.UnderlyingKind.Futures,
            contract.ExerciseStyle == OptionExerciseStyle.American ? Unified.ExerciseKind.American : Unified.ExerciseKind.European,
            contract.SchemaVersion < 3 || contract.PremiumStyle == OptionPremiumStyle.PaidUpfront
                ? Unified.PremiumKind.PaidUpfront : Unified.PremiumKind.FuturesStyle,
            isCall ? Unified.OptionSide.Call : Unified.OptionSide.Put, forward, (double)strike, time, rate);

    /// <summary>Validates immutable reference inputs before worker feed allocation and on each pricing pass.</summary>
    public static OptionPricingFailure? ValidateContext(OptionPricingContext context, DateTimeOffset at)
    {
        if (context.Contract is null || context.Calendar is null || context.Rate is null || context.Rate.Conversion is null)
            return new("PricingContextUnavailable", "Context", "", "Required reference context is absent.");
        var contract = context.Contract;
        OptionPricingFailure Fail(string code, string input) =>
            new(code, input, contract.ContractId, "Option pricing inputs do not satisfy the qualified context.");
        var invalid = OptionPricingQualification.Validate(contract, at);
        if (invalid is not null) return invalid;
        var engine = EngineFor(contract);
        if (context.PricerVersion != engine) return Fail("PricingModelUnsupported", "PricerVersion");
        if (string.IsNullOrWhiteSpace(context.PublicationPolicyVersion) || context.ValidUntilUtc.Offset != TimeSpan.Zero
            || context.ValidUntilUtc <= at || context.Rate.ObservedAtUtc.Offset != TimeSpan.Zero || context.Rate.ObservedAtUtc > at
            || context.Rate.ValueDate > DateOnly.FromDateTime(at.UtcDateTime)) return Fail("TreasuryStale", "ReferenceValidity");
        if (!double.IsFinite(context.Rate.AnnualContinuousRate)
            || context.Rate.Conversion.Convention != Framework.MarketData.Contracts.TreasuryRateConvention.UsTreasuryCmtNominalSemiannual
            || context.Rate.ModelingPolicy != "FlatSelectedCmtProxy/v1"
            || string.IsNullOrWhiteSpace(context.Rate.Conversion.EvidenceId)
            || context.Rate.CurveDigest is not { Length: 64 }
            || context.Rate.RatePercent <= -200
            || context.Rate.AnnualContinuousRate != 2 * double.LogP1((double)(context.Rate.RatePercent / 200m)))
            return Fail("RateConventionUnsupported", "Rate");
        if (context.GenerationId == Guid.Empty)
            return Fail("Recovering", "Generation");
        if (context.MaximumQuoteAgeMilliseconds is < 1 or > 5000 || context.MaximumQuoteSkewMilliseconds is < 0 or > 2000
            || context.MaximumSourceClockLeadMilliseconds is < 0 or > 2000)
            return Fail("InvalidQuotePolicy", "QuoteLimits");
        int days;
        try { days = OptionPricingQualification.CountTradingDays(context.Calendar, contract, at); }
        catch (Exception e) when (e is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { return Fail("CalendarCoverageUnavailable", "Calendar"); }
        var tenor = TreasuryRateConversion.SelectTenor(days);
        if (tenor is null) return Fail("TreasuryHorizonUnsupported", "TradingDays");
        if (tenor != context.Rate.Tenor) return Fail("TreasuryContextChanged", "Tenor");
        return null;
    }
}
