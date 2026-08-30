using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Command;

public sealed class PortfolioFundReplayIntegrationTests
{
    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Fund_history_reloads_the_same_parent_and_terminal_state()
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var source = new PortfolioFundAggregate();
        PortfolioFundDomainEvent[] history =
        [
            source.Create(Guid.NewGuid(), Mandate(now), now, "admin"),
            source.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Retired, "ended", default, now.AddMinutes(1), "admin"),
        ];
        var reloaded = new PortfolioFundAggregate();

        reloaded.Replay(history);

        reloaded.Current.Should().BeEquivalentTo(source.Current);
        reloaded.Revision.Should().Be(2);
    }

    static FundMandateReadModel Mandate(DateTime now) => new()
    {
        PortfolioId = 101, FundId = 205, FundCode = "DAILY", Name = "Daily",
        FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
        EffectiveFromUtc = now, DecisionHorizon = "Daily", Objective = "Directional",
        UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"],
        PermittedTradeFamilies = ["DirectionalFuture"], CreatedOnUtc = now, CreatedBy = "admin",
    };
}
