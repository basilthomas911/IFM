using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record OptionPricingContextResult(OptionPricingContext? Context, OptionPricingFailure? Failure);

/// <summary>Builds immutable reference inputs before a session; never runs from a quote callback.</summary>
public interface IOptionPricingContextProvider
{
    Task<OptionPricingContextResult> PrepareAsync(OptionPricingConvention contract, OptionPricingCalendar calendar,
        TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, Guid generation,
        string pricerVersion, DateTimeOffset at, CancellationToken cancellationToken);
}

public sealed class OptionPricingContextProvider(TreasuryPricingProvider treasury) : IOptionPricingContextProvider
{
    public async Task<OptionPricingContextResult> PrepareAsync(OptionPricingConvention contract, OptionPricingCalendar calendar,
        TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, Guid generation,
        string pricerVersion, DateTimeOffset at, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invalid = OptionPricingQualification.Validate(contract, at);
        if (invalid is not null) return new(null, invalid);
        if (generation == Guid.Empty || pricerVersion is not ("Black76.Managed/v1" or "Black76.Rust/v1"))
            return new(null, new("PricingModelUnsupported", "Generation/Pricer", contract.ContractId, "An exact pricing engine and generation are required."));
        int count;
        try { count = OptionPricingQualification.CountTradingDays(calendar, contract, at); }
        catch (Exception e) when (e is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new(null, new("CalendarCoverageUnavailable", "Calendar", contract.ContractId, "The reviewed calendar does not cover this pricing pass."));
        }
        var rate = await treasury.GetAsync(at, count, publication, conversion, cancellationToken).ConfigureAwait(false);
        if (!rate.Succeeded) return new(null, new(rate.Error!, "Treasury", contract.ContractId, "A qualified daily Treasury rate is required.", true));
        var validUntil = new[] { rate.ValidUntilUtc, contract.LastTradingUtc, contract.ExpirationUtc, contract.EffectiveUntilUtc }.Min();
        if (validUntil <= at) return new(null, new("ExpiredContract", "Validity", contract.ContractId, "Pricing reference context has expired."));
        return new(new(contract, calendar, rate.Rate!, validUntil, generation, pricerVersion, 5000, 2000, publication.Version), null);
    }
}
