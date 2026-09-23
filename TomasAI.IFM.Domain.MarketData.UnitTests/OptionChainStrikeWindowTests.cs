using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class OptionChainStrikeWindowTests
{
    [Fact]
    public void Select_uses_two_point_five_sigma_bounds()
    {
        var definitions = Enumerable.Range(0, 11)
            .SelectMany(index => new[] { Contract("C", 4900 + index * 20), Contract("P", 4900 + index * 20) });

        var result = OptionChainStrikeWindow.Select(definitions, 5000m, 40m, 2.5, 80);

        Assert.Equal(4900m, result.LowerBound);
        Assert.Equal(5100m, result.UpperBound);
        Assert.Equal(22, result.Contracts.Length);
        Assert.Equal("Bollinger2.5Sigma", result.Method);
    }

    [Fact]
    public void Select_retains_required_leg_outside_window()
    {
        var definitions = new[] { Contract("C", 4800), Contract("C", 5000), Contract("C", 5200) };

        var result = OptionChainStrikeWindow.Select(definitions, 5000m, 20m, 2.5, 80,
            [definitions[0].ContractId]);

        Assert.Contains(result.Contracts, value => value.ContractId == definitions[0].ContractId);
        Assert.Contains(result.Contracts, value => value.StrikePrice == 5000);
    }

    [Fact]
    public void Select_falls_back_to_nearest_strikes_when_sigma_is_unavailable()
    {
        var definitions = Enumerable.Range(0, 9).Select(index => Contract("C", 4800 + index * 50));

        var result = OptionChainStrikeWindow.Select(definitions, 5010m, null, 2.5, 3);

        Assert.Equal(new[] { 4950d, 5000d, 5050d }, result.Contracts.Select(x => x.StrikePrice));
        Assert.Equal("NearestStrikesFallback", result.Method);
    }

    [Fact]
    public void Select_does_not_claim_bollinger_bounds_without_a_current_price()
    {
        var definitions = Enumerable.Range(0, 9).Select(index => Contract("C", 4800 + index * 50));

        var result = OptionChainStrikeWindow.Select(definitions, null, 40m, 2.5, 3);

        Assert.Equal("NearestStrikesFallback", result.Method);
        Assert.Equal(3, result.Contracts.Length);
    }

    private static FuturesOptionContractReadModel Contract(string right, double strike) => new(
        $"ES-{right}-{strike}", string.Empty, "ES", "ES", "FOP", "USD", "CME", "50",
        new DateOnly(2026, 12, 18), strike, right);
}
