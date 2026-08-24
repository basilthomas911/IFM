using TomasAI.IFM.UI.Net.Models.OptionPricing;

namespace TomasAI.IFM.UI.Net.Services.OptionPricing;

/// <summary>Defines the UI-facing option-pricing boundary.</summary>
public interface IOptionPricingService
{
    /// <summary>Calculates implied volatility and Black-76 Greeks for a futures option.</summary>
    /// <param name="valueDate">The valuation date.</param>
    /// <param name="maturityDate">The option maturity date.</param>
    /// <param name="optionType">The backend-neutral option type name, such as <c>CALL</c> or <c>PUT</c>.</param>
    /// <param name="assetPrice">The current futures price.</param>
    /// <param name="strikePrice">The option strike price.</param>
    /// <param name="optionValue">The observed option price.</param>
    /// <param name="riskFreeRate">The continuously compounded annual risk-free rate.</param>
    /// <returns>A UI-owned calculation result.</returns>
    OptionGreeksUiModel CalculateGreeks(
        DateOnly valueDate,
        DateOnly maturityDate,
        string optionType,
        double assetPrice,
        double strikePrice,
        double optionValue,
        double riskFreeRate);
}

