using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class PortfolioFundAggregateTests
{
    static readonly DateTime Now = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
    static readonly FundActivationContext ActiveContext = new(true, 1, true, true);

    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Fund_activation_requires_parent_assignment_and_profiles()
    {
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "fund-admin");

        var action = () => aggregate.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Active, "activate", default, Now.AddMinutes(1), "fund-admin");

        action.Should().Throw<InvalidOperationException>().WithMessage("*configuration is incomplete*");
    }

    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Parent_identity_and_code_are_immutable_across_versions()
    {
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), Draft(), Now, "fund-admin");

        var changedParent = () => aggregate.AddVersion(Guid.NewGuid(), 1,
            Draft() with { PortfolioId = 999, FundMandateVersion = 2 }, ActiveContext, Now.AddMinutes(1), "fund-admin");
        var changedCode = () => aggregate.AddVersion(Guid.NewGuid(), 1,
            Draft() with { FundCode = "OTHER", FundMandateVersion = 2 }, ActiveContext, Now.AddMinutes(1), "fund-admin");

        changedParent.Should().Throw<ArgumentException>();
        changedCode.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(FundOperatingState.Draft, FundOperatingState.Active, false, true)]
    [InlineData(FundOperatingState.Disabled, FundOperatingState.Active, false, false)]
    [InlineData(FundOperatingState.Disabled, FundOperatingState.Active, true, true)]
    [InlineData(FundOperatingState.Retired, FundOperatingState.Active, true, false)]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Fund_transition_table_matches_specification(FundOperatingState from, FundOperatingState to, bool throughVersion, bool expected) =>
        PortfolioFundAggregate.CanTransition(from, to, throughVersion).Should().Be(expected);

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void Legacy_history_fund_can_never_be_activated()
    {
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), Draft() with
        {
            HistoricalSource = "FundLegacyDb",
            HistoricalSourceFundId = 1004,
        }, Now, "legacy-import");

        var action = () => aggregate.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Active,
            "must remain read-only", ActiveContext, Now.AddMinutes(1), "fund-admin");

        action.Should().Throw<InvalidOperationException>().WithMessage("*cannot become operational*");
    }

    internal static FundMandateReadModel Draft() => new()
    {
        PortfolioId = 101, FundId = 205, FundCode = "DAILY", Name = "Daily Directional",
        FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
        EffectiveFromUtc = Now, DecisionHorizon = "Daily", Objective = "Directional ES",
        UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"],
        PermittedDirections = ["Bullish", "Bearish"], PermittedConditions = ["Trending"],
        PermittedTradeFamilies = ["DirectionalFuture"], CreatedOnUtc = Now, CreatedBy = "fund-admin",
    };
}
