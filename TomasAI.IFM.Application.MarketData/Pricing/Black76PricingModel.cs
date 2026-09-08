using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.OptionPricer.Black76;

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

/// <summary>Pure European-futures-option calculation over frozen reference and source-time quotes.</summary>
public static class Black76PricingModel
{
    public static OptionPricingPassResult Calculate(OptionPricingContext context, OptionPricingQuote underlying,
        OptionPricingQuote option, decimal strike, bool isCall, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(option);
        var referenceFailure = ValidateContext(context, at);
        if (referenceFailure is not null) return new(null, referenceFailure);
        var contract = context.Contract;
        OptionPricingPassResult Fail(string code, string input) => new(null,
            new(code, input, contract.ContractId, "Option pricing inputs do not satisfy the qualified context."));
        if (underlying.GenerationId != context.GenerationId || option.GenerationId != context.GenerationId)
            return Fail("Recovering", "Generation");
        if (option.ContractId != contract.ContractId || underlying.ContractId != contract.UnderlyingContractId || strike <= 0)
            return Fail("ContractMetadataUnavailable", "Identity/Strike");
        foreach (var quote in new[] { underlying, option })
        {
            if (quote.Bid <= 0 || quote.Ask < quote.Bid || quote.BidSize < 0 || quote.AskSize < 0 || quote.Sequence < 0
                || quote.EventAtUtc.Offset != TimeSpan.Zero || quote.ReceivedAtUtc.Offset != TimeSpan.Zero
                || quote.EventAtUtc > at || quote.ReceivedAtUtc > at)
                return Fail("InvalidQuote", quote.ContractId);
            if ((at - quote.EventAtUtc).TotalMilliseconds > context.MaximumQuoteAgeMilliseconds)
                return Fail("StaleData", quote.ContractId);
        }
        if (Math.Abs((underlying.EventAtUtc - option.EventAtUtc).TotalMilliseconds) > context.MaximumQuoteSkewMilliseconds)
            return Fail("IncoherentQuotes", "SourceTimeSkew");
        var t = OptionPricingQualification.YearFraction(contract, at);
        var forward = (double)(underlying.Bid / 2m + underlying.Ask / 2m);
        var mark = (double)(option.Bid / 2m + option.Ask / 2m);
        var rate = context.Rate.AnnualContinuousRate;
        var greeks = new OptionCalculator(t).GetOptionGreeks(isCall ? "CALL" : "PUT", forward, (double)strike, mark, rate);
        if (!greeks.Success) return Fail("GreeksCalculationFailed", "QuoteMidpoint/IVSolver");
        var price = OptionModel.Price(forward, (double)strike, rate, greeks.ImpliedVolatility, t, isCall ? 1 : -1);
        var digest = PricingSemanticHash.Compute(new { Version = 1, context, underlying, option, strike, isCall, at, t });
        return new(new(greeks.ImpliedVolatility, greeks.Delta, greeks.Gamma, greeks.Theta,
            greeks.Vega, greeks.Rho, price, t, digest), null);
    }

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
        var engine = OptionCalculator.EngineVersion;
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
        if (context.MaximumQuoteAgeMilliseconds is < 1 or > 5000 || context.MaximumQuoteSkewMilliseconds is < 0 or > 2000)
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
