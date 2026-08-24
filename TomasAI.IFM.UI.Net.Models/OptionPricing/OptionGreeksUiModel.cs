namespace TomasAI.IFM.UI.Net.Models.OptionPricing;

/// <summary>Represents UI-owned implied-volatility and option-Greek output.</summary>
/// <param name="Success">Whether the option calculation produced finite values.</param>
/// <param name="ImpliedVolatility">The calculated implied volatility.</param>
/// <param name="Delta">The calculated delta.</param>
/// <param name="Gamma">The calculated gamma.</param>
/// <param name="Theta">The calculated theta.</param>
/// <param name="Vega">The calculated vega.</param>
/// <param name="Rho">The calculated rho.</param>
public sealed record OptionGreeksUiModel(
    bool Success,
    double ImpliedVolatility,
    double Delta,
    double Gamma,
    double Theta,
    double Vega,
    double Rho);

