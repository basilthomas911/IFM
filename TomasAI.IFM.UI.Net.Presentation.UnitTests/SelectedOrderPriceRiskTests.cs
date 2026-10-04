using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests;

public sealed class SelectedOrderPriceRiskTests
{
    [Theory]
    [InlineData(1, -1, 1, 50)]
    [InlineData(-1, 1, -1, 450)]
    [InlineData(2, -2, 1, 100)]
    [InlineData(-2, 2, -1, 900)]
    public void Vertical_spread_loss_uses_actual_width_price_and_quantity(int first, int second, decimal price, decimal expected)
    {
        TradeLegDefinition[] legs = [new() { Strike = 100m, PutCall = 1, SignedQuantity = first, CashMultiplier = 50m },
            new() { Strike = 110m, PutCall = 1, SignedQuantity = second, CashMultiplier = 50m }];
        Assert.Equal(expected, SelectedOrderPriceRisk.MaximumOptionLoss(legs, price, Math.Abs(first)));
    }
    [Fact]
    public void Default_execution_settings_preserve_historical_financial_hash_and_nondefaults_change_it()
    {
        var candidate = new TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioOrderCandidate
        { ValidUntilUtc = DateTime.UtcNow };
        var json = System.Text.Json.JsonSerializer.SerializeToElement(candidate);
        var old = json.EnumerateObject().Where(x => x.Name is not ("TimeInForce" or "AlgorithmPace"))
            .ToDictionary(x => x.Name, x => x.Value);
        var hash = TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(candidate);
        Assert.Equal(hash, TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(old));
        Assert.NotEqual(hash, TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(candidate with { TimeInForce = "GTC" }));
        Assert.NotEqual(hash, TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(candidate with { AlgorithmPace = "Urgent" }));
    }

    [Fact]
    public void Condor_loss_changes_with_the_submitted_credit()
    {
        TradeLegDefinition[] legs = [new() { Strike = 95m, PutCall = 2, SignedQuantity = 1, CashMultiplier = 50m },
            new() { Strike = 100m, PutCall = 2, SignedQuantity = -1, CashMultiplier = 50m },
            new() { Strike = 110m, PutCall = 1, SignedQuantity = -1, CashMultiplier = 50m },
            new() { Strike = 115m, PutCall = 1, SignedQuantity = 1, CashMultiplier = 50m }];
        Assert.Equal(150m, SelectedOrderPriceRisk.MaximumOptionLoss(legs, -2m, 1));
        Assert.Equal(147.5m, SelectedOrderPriceRisk.MaximumOptionLoss(legs, -2.05m, 1));
    }
}
