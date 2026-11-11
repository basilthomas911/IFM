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

    [Fact]
    public void Given_saved_Futures_defaults_when_replayed_then_symbols_and_cycles_are_retained_without_changing_mandate_version()
    {
        var fund = new PortfolioFundAggregate();
        fund.Create(Guid.NewGuid(), Draft() with { SchemaVersion=3, PermittedTradeFamilies=[] }, Now, "fund-admin");
        var replacement = Draft() with { FundMandateVersion=2, SchemaVersion=4, PermittedTradeFamilies=[],
            UnderlyingUniverse=["ES","VX"], BaseContracts=[new(){Symbol="ES",ForwardCycles=2},new(){Symbol="VX",ForwardCycles=1}] };
        fund.ConfigureBaseContracts(Guid.NewGuid(),1,replacement with { FundMandateVersion=1 },Now.AddMinutes(1),"fund-admin");
        var restored = new PortfolioFundAggregate();
        restored.RestoreSnapshot(fund.CaptureSnapshot());
        restored.Current!.BaseContracts.Should().BeEquivalentTo(replacement.BaseContracts);
        restored.Current.FundMandateVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Given_a_Futures_configuration_when_no_symbol_or_Equities_is_saved_then_it_is_rejected(bool equities)
    {
        var fund = new PortfolioFundAggregate();
        fund.Create(Guid.NewGuid(),Draft(),Now,"fund-admin");
        var replacement = Draft() with { FundMandateVersion=2, SchemaVersion=4, PermittedTradeFamilies=[],
            BaseContracts=equities?[new(){BaseAsset="Equities",Symbol="ES"}]:[] };
        var save=()=>fund.AddVersion(Guid.NewGuid(),1,replacement,default,Now.AddMinutes(1),"fund-admin");
        save.Should().Throw<ArgumentException>();
        fund.Revision.Should().Be(1);
    }

    static FundMandateReadModel Draft() => new()
    {
        PortfolioId = 101,
        FundId = 205,
        FundCode = "DAILY",
        Name = "Daily Directional",
        FundMandateVersion = 1,
        TradingYear = 2026,
        OperatingState = FundOperatingState.Draft,
        EffectiveFromUtc = Now,
        DecisionHorizon = "Daily",
        Objective = "Directional ES",
        UnderlyingUniverse = ["ES"],
        EligibleAssetTypes = ["Futures"],
        PermittedTradeFamilies = ["DirectionalFuture"],
        CreatedOnUtc = Now,
        CreatedBy = "fund-admin",
    };
}
