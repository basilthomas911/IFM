using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Operations;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Operations;

public sealed class PortfolioOperationalPolicyTests
{
    public static IEnumerable<object[]> AuthorizationMatrix()
    {
        var personas = new[]
        {
            ("Anonymous", Array.Empty<string>()),
            (PortfolioOperationalPolicy.ReaderRole, new[] { PortfolioOperationalPolicy.ReaderRole }),
            (PortfolioOperationalPolicy.AdministratorRole, new[] { PortfolioOperationalPolicy.AdministratorRole }),
            (PortfolioOperationalPolicy.WorkflowRole, new[] { PortfolioOperationalPolicy.WorkflowRole }),
        };

        foreach (var operation in Enum.GetValues<PortfolioOperation>())
        foreach (var (persona, roles) in personas)
        {
            var expected = persona switch
            {
                "Anonymous" => false,
                PortfolioOperationalPolicy.ReaderRole => operation == PortfolioOperation.Read,
                PortfolioOperationalPolicy.AdministratorRole => operation is PortfolioOperation.Read or
                    PortfolioOperation.AdministerPortfolio or PortfolioOperation.AdministerFund or
                    PortfolioOperation.DelegateAllocation or PortfolioOperation.DelegateRiskEnvelope or
                    PortfolioOperation.AssignTemplate,
                PortfolioOperationalPolicy.WorkflowRole => operation is PortfolioOperation.Read or
                    PortfolioOperation.ReserveComposition or PortfolioOperation.RecordCompositionResult or
                    PortfolioOperation.RecordRiskResult,
                _ => false,
            };
            yield return [operation, roles, expected];
        }
    }

    [Theory]
    [MemberData(nameof(AuthorizationMatrix))]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Every_bounded_operation_obeys_the_complete_least_privilege_persona_matrix(
        PortfolioOperation operation,
        string[] roles,
        bool expected)
    {
        PortfolioOperationalPolicy.IsAuthorized(operation, roles.ToHashSet(StringComparer.Ordinal)).Should().Be(expected);
    }

    [Fact]
    [Trait("Gate", "PF-20")]
    [Trait("Gate", "PF-29")]
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
