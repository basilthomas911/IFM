using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class PortfolioAggregateTests
{
    static readonly DateTime Now = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Lifecycle_enforces_versions_transitions_and_terminal_retirement()
    {
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "test-admin");
        aggregate.AddVersion(Guid.NewGuid(), 1, ActiveVersion(), Now.AddMinutes(1), "test-admin");
        aggregate.ChangeState(Guid.NewGuid(), 2, PortfolioOperatingState.Paused, "operator pause", Now.AddMinutes(2), "test-admin");
        aggregate.ChangeState(Guid.NewGuid(), 3, PortfolioOperatingState.Active, "resume", Now.AddMinutes(3), "test-admin");
        aggregate.Retire(Guid.NewGuid(), 4, "closed", Now.AddMinutes(4), "test-admin");

        aggregate.Revision.Should().Be(5);
        aggregate.Current!.OperatingState.Should().Be(PortfolioOperatingState.Retired);
        var action = () => aggregate.ChangeState(Guid.NewGuid(), 5, PortfolioOperatingState.Active, "invalid", Now.AddMinutes(5), "test-admin");
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Fund_membership_requires_matching_parent_and_is_unique()
    {
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "test-admin");
        aggregate.AddFund(Guid.NewGuid(), 1, new PortfolioFundId(101, 205), Now.AddMinutes(1), "test-admin");

        aggregate.FundIds.Should().ContainSingle().Which.Should().Be(205);
        var duplicate = () => aggregate.AddFund(Guid.NewGuid(), 2, new PortfolioFundId(101, 205), Now.AddMinutes(2), "test-admin");
        duplicate.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(PortfolioOperatingState.Draft, PortfolioOperatingState.Active, false, true)]
    [InlineData(PortfolioOperatingState.Disabled, PortfolioOperatingState.Active, false, false)]
    [InlineData(PortfolioOperatingState.Disabled, PortfolioOperatingState.Active, true, true)]
    [InlineData(PortfolioOperatingState.Retired, PortfolioOperatingState.Active, true, false)]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Transition_table_matches_specification(PortfolioOperatingState from, PortfolioOperatingState to, bool throughVersion, bool expected) =>
        PortfolioAggregate.CanTransition(from, to, throughVersion).Should().Be(expected);

    internal static PortfolioReadModel Draft() => new()
    {
        PortfolioId = 101,
        PortfolioCode = "CORE",
        Name = "Core Portfolio",
        PortfolioVersion = 1,
        BaseCurrency = "USD",
        OperatingState = PortfolioOperatingState.Draft,
        EffectiveFromUtc = Now,
        CreatedOnUtc = Now,
        CreatedBy = "test-admin",
    };

    internal static PortfolioReadModel ActiveVersion() => Draft() with
    {
        PortfolioVersion = 2,
        OperatingState = PortfolioOperatingState.Active,
        PolicyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        PolicyVersion = 1,
        BrokerAccountRefs = ["paper-primary"],
    };
}
