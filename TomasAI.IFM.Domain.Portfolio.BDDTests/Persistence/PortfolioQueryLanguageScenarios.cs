using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioDb;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Persistence;

public sealed class PortfolioQueryLanguageScenarios
{
    [Fact]
    [Trait("Gate", "PF-08")]
    public void Given_operator_navigation_then_each_relationship_has_a_direct_bounded_query_language()
    {
        string[] journeys =
        [
            PortfolioDbCql.GetPortfolio, PortfolioDbCql.GetPortfoliosByState,
            PortfolioDbCql.GetFundsByPortfolio, PortfolioDbCql.GetFund,
            PortfolioDbCql.GetActiveFunds, PortfolioDbCql.GetAssignments,
            PortfolioDbCql.GetEnvelope, PortfolioDbCql.GetOrders,
            PortfolioDbCql.GetOrder, PortfolioDbCql.GetOrderTrades,
            PortfolioDbCql.GetTrade, PortfolioDbCql.GetCompositions
        ];
        journeys.Should().OnlyContain(x => x.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        journeys.Should().OnlyContain(x => !x.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase));
    }
}
