using FluentAssertions;
using TomasAI.IFM.UI.Net.Services.OptionPricing;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

/// <summary>Verifies the UI-facing option-pricing mapping boundary.</summary>
public sealed class OptionPricingServiceTests
{
    /// <summary>Ensures a valid option price maps finite framework output to the UI model.</summary>
    [Fact]
    public void CalculateGreeks_WithValidInput_ReturnsSuccessfulFiniteUiResult()
    {
        var service = new OptionPricingService();

        var result = service.CalculateGreeks(
            new DateOnly(2026, 8, 24),
            new DateOnly(2026, 12, 18),
            "CALL",
            6500.0,
            6550.0,
            120.0,
            0.04);

        result.Success.Should().BeTrue();
        result.ImpliedVolatility.Should().BePositive();
        new[] { result.Delta, result.Gamma, result.Theta, result.Vega, result.Rho }
            .Should().OnlyContain(value => double.IsFinite(value));
    }

    /// <summary>Ensures invalid market input is represented as an unsuccessful UI result.</summary>
    [Fact]
    public void CalculateGreeks_WithInvalidPrice_ReturnsFailedUiResult()
    {
        var result = new OptionPricingService().CalculateGreeks(
            new DateOnly(2026, 8, 24),
            new DateOnly(2026, 12, 18),
            "PUT",
            6500.0,
            6550.0,
            0.0,
            0.04);

        result.Success.Should().BeFalse();
    }
}
