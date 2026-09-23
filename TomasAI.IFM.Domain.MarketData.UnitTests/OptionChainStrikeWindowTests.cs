using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class OptionChainStrikeWindowTests
{
    /// <summary>Checks the seven-day ES example with five-delta Z and a 15 percent buffer.</summary>
    [Fact]
    public void SelectImpliedVolatility_uses_fractional_expiry_time_and_buffer()
    {
        var at = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var definitions = Enumerable.Range(0, 161).Select(i => Contract("C", 7422 + i * 5));

        var result = OptionChainStrikeWindow.SelectImpliedVolatility(
            definitions, 7822m, 0.16, at.AddDays(7), at);

        Assert.Equal("ImpliedVolatility5Delta", result.Method);
        Assert.True(result.Contracts.Select(x => x.StrikePrice).Distinct().Count() > 80);
        Assert.All(result.Contracts, x => Assert.InRange(x.StrikePrice, 7492d, 8152d));
    }

    /// <summary>Checks un-clipped bounds and retention of a staged leg outside the estimated window.</summary>
    [Fact]
    public void SelectImpliedVolatility_retains_required_leg()
    {
        var at = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var definitions = Enumerable.Range(0, 61).Select(i => Contract("C", 7672 + i * 5)).ToArray();

        var result = OptionChainStrikeWindow.SelectImpliedVolatility(definitions,
            7822m, 0.16, at.AddDays(1), at,
            requiredContractIds:[definitions[0].ContractId]);

        Assert.Equal("ImpliedVolatility5Delta", result.Method);
        Assert.Contains(result.Contracts, x => x.ContractId == definitions[0].ContractId);
        Assert.InRange(result.LowerBound!.Value, 7680m, 7820m);
        Assert.InRange(result.UpperBound!.Value, 7825m, 7970m);
    }

    [Fact]
    public void SelectImpliedVolatility_does_not_truncate_a_window_above_512_contracts()
    {
        var at = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var definitions = Enumerable.Range(0, 300).SelectMany(i => new[]
        {
            Contract("C", 4700 + i * 2), Contract("P", 4700 + i * 2)
        });

        var result = OptionChainStrikeWindow.SelectImpliedVolatility(
            definitions, 5000m, 0.30, at.AddDays(30), at);

        Assert.Equal("ImpliedVolatility5Delta", result.Method);
        Assert.Equal(600, result.Contracts.Length);
        Assert.Equal(300, result.Contracts.Select(x => x.StrikePrice).Distinct().Count());
    }

    [Fact]
    public void Select_uses_two_point_five_sigma_bounds()
    {
        var definitions = Enumerable.Range(0, 11)
            .SelectMany(index => new[] { Contract("C", 4900 + index * 20), Contract("P", 4900 + index * 20) });

        var result = OptionChainStrikeWindow.Select(definitions, 5000m, 40m, 2.5);

        Assert.Equal(4900m, result.LowerBound);
        Assert.Equal(5100m, result.UpperBound);
        Assert.Equal(22, result.Contracts.Length);
        Assert.Equal("Bollinger2.5Sigma", result.Method);
    }

    [Fact]
    public void Select_retains_required_leg_outside_window()
    {
        var definitions = new[] { Contract("C", 4800), Contract("C", 5000), Contract("C", 5200) };

        var result = OptionChainStrikeWindow.Select(definitions, 5000m, 20m, 2.5,
            [definitions[0].ContractId]);

        Assert.Contains(result.Contracts, value => value.ContractId == definitions[0].ContractId);
        Assert.Contains(result.Contracts, value => value.StrikePrice == 5000);
    }

    [Fact]
    public void Select_requires_bollinger_sigma_when_iv_is_unavailable()
    {
        var definitions = Enumerable.Range(0, 9).Select(index => Contract("C", 4800 + index * 50));

        var result = OptionChainStrikeWindow.Select(definitions, 5010m, null, 2.5);

        Assert.Empty(result.Contracts);
        Assert.Equal("WindowInputsUnavailable", result.Method);
    }

    [Fact]
    public void Select_does_not_claim_bollinger_bounds_without_a_current_price()
    {
        var definitions = Enumerable.Range(0, 9).Select(index => Contract("C", 4800 + index * 50));

        var result = OptionChainStrikeWindow.Select(definitions, null, 40m, 2.5);

        Assert.Equal("WindowInputsUnavailable", result.Method);
        Assert.Empty(result.Contracts);
    }

    private static FuturesOptionContractReadModel Contract(string right, double strike) => new(
        $"ES-{right}-{strike}", string.Empty, "ES", "ES", "FOP", "USD", "CME", "50",
        new DateOnly(2026, 12, 18), strike, right);
}
