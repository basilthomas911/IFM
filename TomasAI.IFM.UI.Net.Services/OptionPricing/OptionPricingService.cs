using TomasAI.IFM.Framework.OptionPricer.Pricing;
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
        var request = new OptionPricingRequest(
            UnderlyingKind.Futures,
            ExerciseKind.European,
            PremiumKind.PaidUpfront,
            optionType switch { "CALL" => OptionSide.Call, "PUT" => OptionSide.Put, _ => OptionSide.Unknown },
            assetPrice,
            strikePrice,
            (maturityDate.DayNumber - valueDate.DayNumber) / 365d,
            riskFreeRate);
        var result = new OptionCalculator().ImpliedVolatility(request, optionValue);
        var values = result.Value;
        return new OptionGreeksUiModel(
            result.Success,
            values?.Volatility ?? 0,
            values?.Delta ?? 0,
            values?.Gamma ?? 0,
            values?.Theta ?? 0,
            values?.Vega ?? 0,
            values?.Rho ?? 0);
    }
}

