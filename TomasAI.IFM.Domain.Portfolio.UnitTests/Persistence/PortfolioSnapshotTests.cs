using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Persistence;

public sealed class PortfolioSnapshotTests
{
    [Fact]
    [Trait("Gate", "PF-07")]
    public void Snapshot_restores_state_revision_membership_and_command_deduplication()
    {
        var now = new DateTime(2026, 8, 29, 17, 0, 0, DateTimeKind.Utc);
        var commandId = Guid.NewGuid();
        var source = new PortfolioAggregate();
        source.Create(commandId, Portfolio(now), now, "unit");
        source.AddFund(Guid.NewGuid(), 1, new PortfolioFundId(101, 205), now.AddSeconds(1), "unit");
        var restored = new PortfolioAggregate();

        restored.RestoreSnapshot(source.CaptureSnapshot());

        restored.Current.Should().BeEquivalentTo(source.Current);
        restored.FundIds.Should().Equal(source.FundIds);
        restored.Revision.Should().Be(2);
        FluentActions.Invoking(() => restored.ChangeState(commandId, 2, PortfolioOperatingState.Disabled, "duplicate", now.AddSeconds(2), "unit"))
            .Should().Throw<InvalidOperationException>().WithMessage("*already applied*");
    }

    static PortfolioReadModel Portfolio(DateTime now) => new()
    {
        PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
        OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "unit"
    };
}
