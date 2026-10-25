using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Builds immutable reference inputs before a session; never runs from a quote callback.</summary>
public interface IOptionPricingContextProvider
{
    Task<OptionPricingContextResult> PrepareAsync(OptionPricingConvention contract, OptionPricingCalendar calendar,
        TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, Guid generation,
        string pricerVersion, DateTimeOffset at, CancellationToken cancellationToken);
}
