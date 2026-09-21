using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

public sealed class ProportionalCloseValuationTests
{
    [Theory]
    [InlineData(1000, 500)]
    [InlineData(-1000, -500)]
    [InlineData(0, 0)]
    [InlineData(0.01, 0.01)]
    [InlineData(-0.01, -0.01)]
    public void Partial_close_retains_proportional_signed_MTM_and_final_close_clears_remainder(decimal prior, decimal expected)
    {
        var remaining=ProportionalCloseValuation.Remaining(prior,[new(2,1)]);
        remaining.Should().Be(expected);
        ProportionalCloseValuation.Remaining(remaining,[new(1,1)]).Should().Be(0);
    }

    [Fact]
    public void Multi_leg_ratio_uses_each_legs_remaining_quantity_and_ignores_already_closed_legs()
    {
        ProportionalCloseValuation.Remaining(900,[new(3,1),new(6,2),new(0,0)]).Should().Be(600);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    public void Uneven_multi_leg_closes_require_leg_level_valuation(decimal remaining, decimal closing)
    {
        FluentActions.Invoking(()=>ProportionalCloseValuation.Remaining(100,[new(2,1),new(remaining,closing)]))
            .Should().Throw<InvalidOperationException>().WithMessage("*NON_PROPORTIONAL_CLOSE*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Net_zero_MTM_does_not_authorize_an_uneven_leg_close(decimal prior)
    {
        FluentActions.Invoking(()=>ProportionalCloseValuation.Remaining(prior,[new(2,1),new(2,0)]))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(1, 0)]
    public void Invalid_quantities_fail_closed(decimal remaining, decimal closing)
    {
        FluentActions.Invoking(()=>ProportionalCloseValuation.Remaining(100,[new(remaining,closing)]))
            .Should().Throw<ArgumentException>();
    }
}
