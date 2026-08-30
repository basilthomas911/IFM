using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Command;

public sealed class PortfolioLifecycleScenarios
{
    static readonly DateTime Now = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Given_a_draft_Portfolio_when_a_valid_policy_version_is_added_then_it_can_be_active()
    {
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "portfolio-admin");

        aggregate.AddVersion(Guid.NewGuid(), 1, Draft() with
        {
            PortfolioVersion = 2,
            OperatingState = PortfolioOperatingState.Active,
            PolicyId = Guid.NewGuid(),
            PolicyVersion = 1,
            BrokerAccountRefs = ["paper-primary"],
        }, Now.AddMinutes(1), "portfolio-admin");

        aggregate.Current!.OperatingState.Should().Be(PortfolioOperatingState.Active);
    }

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Given_a_retired_Portfolio_when_a_change_is_requested_then_history_remains_terminal()
    {
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "portfolio-admin");
        aggregate.Retire(Guid.NewGuid(), 1, "mandate ended", Now.AddMinutes(1), "portfolio-admin");

        var action = () => aggregate.AddVersion(Guid.NewGuid(), 2, Draft() with { PortfolioVersion = 2 }, Now.AddMinutes(2), "portfolio-admin");

        action.Should().Throw<InvalidOperationException>();
        aggregate.Current!.OperatingState.Should().Be(PortfolioOperatingState.Retired);
    }

    static PortfolioReadModel Draft() => new()
    {
        PortfolioId = 101, PortfolioCode = "CORE", Name = "Core", PortfolioVersion = 1,
        OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = Now,
        CreatedOnUtc = Now, CreatedBy = "portfolio-admin",
    };
}
