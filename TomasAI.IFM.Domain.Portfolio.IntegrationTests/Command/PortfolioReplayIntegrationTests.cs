using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Command;

public sealed class PortfolioReplayIntegrationTests
{
    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Event_history_reloads_identical_Portfolio_state()
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var source = new PortfolioAggregate();
        PortfolioDomainEvent[] history =
        [
            source.Create(Guid.NewGuid(), new PortfolioReadModel
            {
                PortfolioId = 101, PortfolioCode = "CORE", Name = "Core", PortfolioVersion = 1,
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "admin",
            }, now, "admin"),
            source.Retire(Guid.NewGuid(), 1, "closed", now.AddMinutes(1), "admin"),
        ];
        var reloaded = new PortfolioAggregate();

        reloaded.Replay(history);

        reloaded.Current.Should().BeEquivalentTo(source.Current);
        reloaded.Revision.Should().Be(source.Revision);
    }
}
