using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Operations;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Operations;

public sealed class PortfolioOperationalPolicyTests
{
    [Fact]
    [Trait("Gate", "PF-20")]
    [Trait("Category", "Portfolio")]
    public void Roles_are_least_privilege_and_execution_authority_is_absent()
    {
        PortfolioOperationalPolicy.IsAuthorized(PortfolioOperation.Read, new HashSet<string> { PortfolioOperationalPolicy.ReaderRole }).Should().BeTrue();
        PortfolioOperationalPolicy.IsAuthorized(PortfolioOperation.AdministerPortfolio, new HashSet<string> { PortfolioOperationalPolicy.ReaderRole }).Should().BeFalse();
        PortfolioOperationalPolicy.IsAuthorized(PortfolioOperation.ReserveComposition, new HashSet<string> { PortfolioOperationalPolicy.WorkflowRole }).Should().BeTrue();
        Enum.GetNames<PortfolioOperation>().Should().NotContain(name => name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Gate", "PF-20")]
    [Trait("Category", "Portfolio")]
    public void Telemetry_contract_has_bounded_names_and_redacts_hashes()
    {
        PortfolioOperationalPolicy.RequiredTraceFields.Should().OnlyHaveUniqueItems();
        PortfolioOperationalPolicy.BoundedMetricNames.Should().OnlyHaveUniqueItems().And.OnlyContain(name => !name.Contains("id", StringComparison.OrdinalIgnoreCase));
        PortfolioOperationalPolicy.RedactHash(new string('a', 64)).Should().Be("aaaaaaaa…aaaa");
    }
}
