using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Legacy;

public sealed class LegacyPortfolioHistoryScenarios
{
    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void Given_an_imported_legacy_fund_when_activation_is_requested_then_history_remains_read_only()
    {
        var now = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), new FundMandateReadModel
        {
            PortfolioId = 1101, FundId = 5001, FundCode = "LEGACY-1004", Name = "Legacy Fund",
            FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
            EffectiveFromUtc = now, DecisionHorizon = "LegacyHistory", Objective = "Read-only history",
            UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Options"], PermittedTradeFamilies = ["IRON_CONDOR"],
            CreatedOnUtc = now, CreatedBy = "legacy-import", HistoricalSource = "FundLegacyDb", HistoricalSourceFundId = 1004,
        }, now, "legacy-import");

        var action = () => aggregate.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Active, "activate",
            new FundActivationContext(true, 1, true, true), now.AddMinutes(1), "operator");

        action.Should().Throw<InvalidOperationException>().WithMessage("*cannot become operational*");
        aggregate.Current!.OperatingState.Should().Be(FundOperatingState.Draft);
    }
}
