using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Command;

public sealed class PortfolioLifecycleVerificationTests
{
    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Production_state_machine_preserves_lifecycle_and_membership_attribution()
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = 101, PortfolioCode = "CORE", Name = "Core", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
            CreatedOnUtc = now, CreatedBy = "verification",
        }, now, "verification");
        aggregate.AddFund(Guid.NewGuid(), 1, new PortfolioFundId(101, 205), now.AddMinutes(1), "verification");
        aggregate.Retire(Guid.NewGuid(), 2, "completed", now.AddMinutes(2), "verification");

        aggregate.Current!.PortfolioId.Should().Be(101);
        aggregate.FundIds.Should().BeEquivalentTo(new[] { 205 });
        aggregate.Current.OperatingState.Should().Be(PortfolioOperatingState.Retired);
    }
}
