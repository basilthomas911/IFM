using System.Reflection;
using FluentAssertions;

using TomasAI.IFM.Application.Storage.PortfolioDb;



namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Persistence;



public sealed class PortfolioQueryLanguageScenarios

{

    [Fact]

    [Trait("Gate", "PF-08")]

    public void Given_operator_navigation_then_each_relationship_has_a_direct_bounded_query_language()

    {
        static string Query(string name) => (string)typeof(PortfolioDbSql)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;


        string[] journeys =

        [

            Query("Portfolio_Get"), Query("Portfolio_ByState"),

            Query("Fund_ByPortfolio"), Query("Fund_Get"),

            Query("Fund_Active"), Query("Fund_Assignments"),

            Query("Fund_Envelope"), Query("Orders_Timeline"),

            Query("Orders_Get"), Query("Orders_Trades"),

            Query("Orders_Trade"), Query("Orders_Compositions")

        ];

        journeys.Should().OnlyContain(x => x.Contains("WHERE", StringComparison.OrdinalIgnoreCase));

        journeys.Should().OnlyContain(x => x.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)

            || x.Contains("order_id=$1", StringComparison.OrdinalIgnoreCase)

            || x.Contains("trade_id=$1", StringComparison.OrdinalIgnoreCase));

        journeys.Should().OnlyContain(x => !x.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase));

    }

}
