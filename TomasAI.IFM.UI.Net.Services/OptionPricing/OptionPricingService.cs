using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.UI.Net.Models.OptionPricing;

namespace TomasAI.IFM.UI.Net.Services.OptionPricing;

/// <summary>Maps the framework Black-76 calculator to UI-owned pricing output.</summary>
public sealed class OptionPricingService : IOptionPricingService
{
    /// <inheritdoc />
    public OptionGreeksUiModel CalculateGreeks(
        DateOnly valueDate,
        DateOnly maturityDate,
        string optionType,
        double assetPrice,
        double strikePrice,
        double optionValue,
        double riskFreeRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionType);
        var result = new OptionCalculator(valueDate, maturityDate).GetOptionGreeks(
            optionType,
            assetPrice,
            strikePrice,
            optionValue,
            riskFreeRate);
        return new OptionGreeksUiModel(
            result.Success,
            result.ImpliedVolatility,
            result.Delta,
            result.Gamma,
            result.Theta,
            result.Vega,
            result.Rho);
    }
}

