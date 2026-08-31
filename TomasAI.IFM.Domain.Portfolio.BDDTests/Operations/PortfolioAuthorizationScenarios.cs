using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Operations;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Operations;

public sealed class PortfolioAuthorizationScenarios
{
    public static IEnumerable<object[]> Personas()
    {
        yield return ["anonymous", Array.Empty<string>(), Array.Empty<PortfolioOperation>()];
        yield return [PortfolioOperationalPolicy.ReaderRole, new[] { PortfolioOperationalPolicy.ReaderRole },
            new[] { PortfolioOperation.Read }];
        yield return [PortfolioOperationalPolicy.AdministratorRole, new[] { PortfolioOperationalPolicy.AdministratorRole },
            new[]
            {
                PortfolioOperation.Read, PortfolioOperation.AdministerPortfolio, PortfolioOperation.AdministerFund,
                PortfolioOperation.DelegateAllocation, PortfolioOperation.DelegateRiskEnvelope, PortfolioOperation.AssignTemplate,
            }];
        yield return [PortfolioOperationalPolicy.WorkflowRole, new[] { PortfolioOperationalPolicy.WorkflowRole },
            new[]
            {
                PortfolioOperation.Read, PortfolioOperation.ReserveComposition,
                PortfolioOperation.RecordCompositionResult, PortfolioOperation.RecordRiskResult,
            }];
    }

    [Theory]
    [MemberData(nameof(Personas))]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Given_a_bounded_persona_when_operations_are_evaluated_then_only_its_explicit_authority_is_available(
        string persona,
        string[] roles,
        PortfolioOperation[] expected)
    {
        var granted = Enum.GetValues<PortfolioOperation>()
            .Where(operation => PortfolioOperationalPolicy.IsAuthorized(operation, roles.ToHashSet(StringComparer.Ordinal)))
            .ToArray();

        granted.Should().Equal(expected, $"{persona} must receive exactly its documented Portfolio authority");
        Enum.GetNames<PortfolioOperation>().Should().NotContain(name =>
            name.Contains("Execute", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Broker", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fill", StringComparison.OrdinalIgnoreCase));
    }
}
