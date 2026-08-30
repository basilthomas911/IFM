using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Command;

public sealed class PortfolioFundLifecycleScenarios
{
    static readonly DateTime Now = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Given_a_valid_Daily_mandate_when_activated_then_the_parent_identity_is_retained()
    {
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "fund-admin");
        aggregate.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Active, "ready",
            new(true, 1, true, true), Now.AddMinutes(1), "fund-admin");

        aggregate.Current!.OperatingState.Should().Be(FundOperatingState.Active);
        aggregate.Current.PortfolioId.Should().Be(101);
        aggregate.Current.FundId.Should().Be(205);
    }

    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Given_a_retired_Fund_when_reactivation_is_requested_then_it_remains_terminal()
    {
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "fund-admin");
        aggregate.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Retired, "ended", default, Now.AddMinutes(1), "fund-admin");

        var action = () => aggregate.ChangeState(Guid.NewGuid(), 2, FundOperatingState.Active, "invalid",
            new(true, 1, true, true), Now.AddMinutes(2), "fund-admin");

        action.Should().Throw<InvalidOperationException>();
    }

    static FundMandateReadModel Draft() => new()
    {
        PortfolioId = 101, FundId = 205, FundCode = "DAILY", Name = "Daily Directional",
        FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
        EffectiveFromUtc = Now, DecisionHorizon = "Daily", Objective = "Directional ES",
        UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"],
        PermittedTradeFamilies = ["DirectionalFuture"], CreatedOnUtc = Now, CreatedBy = "fund-admin",
    };
}
