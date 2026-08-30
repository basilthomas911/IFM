using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Contracts;

public sealed class PortfolioIdentityScenarios
{
    [Fact]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void Given_an_operator_views_a_trade_identity_then_the_complete_hierarchy_is_visible()
    {
        var identity = new PortfolioFundOrderTradeId(101, 205, 3001, 4001);

        identity.Validate().Should().BeEmpty();
        identity.Format().Should().Be("101.205.3001.4001");
    }

    [Fact]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void Given_any_identity_component_is_not_positive_then_the_identity_is_invalid()
    {
        var identity = new PortfolioFundOrderTradeId(101, 0, 3001, -1);

        identity.Validate().Should().BeEquivalentTo(
            "PortfolioFundOrderTradeId.FundId must be greater than zero.",
            "PortfolioFundOrderTradeId.TradeId must be greater than zero.");
    }
}
