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
            PortfolioDbSql.Portfolio.Get, PortfolioDbSql.Portfolio.ByState,
            PortfolioDbSql.Fund.ByPortfolio, PortfolioDbSql.Fund.Get,
            PortfolioDbSql.Fund.Active, PortfolioDbSql.Fund.Assignments,
            PortfolioDbSql.Fund.Envelope, PortfolioDbSql.Orders.Timeline,
            PortfolioDbSql.Orders.Get, PortfolioDbSql.Orders.Trades,
            PortfolioDbSql.Orders.Trade, PortfolioDbSql.Orders.Compositions
        ];
        journeys.Should().OnlyContain(x => x.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        journeys.Should().OnlyContain(x => x.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
            || x.Contains("order_id=$1",StringComparison.OrdinalIgnoreCase)
            || x.Contains("trade_id=$1",StringComparison.OrdinalIgnoreCase));
        journeys.Should().OnlyContain(x => !x.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase));
    }
}
