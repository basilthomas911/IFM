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
            ActivePolicyId = 9001,
            ActivePolicyVersion = 1,
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

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Given_a_never_activated_draft_when_the_administrator_deletes_it_then_an_audited_terminal_tombstone_is_created()
    {
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "portfolio-admin");

        var result = aggregate.DeleteDraft(Guid.NewGuid(), 1, "duplicate draft", Now.AddMinutes(1), "portfolio-admin");

        result.Should().BeOfType<TomasAI.IFM.Domain.Portfolio.Command.Model.DraftPortfolioDeleted>();
        aggregate.IsDeleted.Should().BeTrue();
        aggregate.Current!.PortfolioId.Should().Be(101, "the consumed sequence ID remains in authoritative history");
    }

    static PortfolioReadModel Draft() => new()
    {
        PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
        OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = Now,
        CreatedOnUtc = Now, CreatedBy = "portfolio-admin",
    };
}
