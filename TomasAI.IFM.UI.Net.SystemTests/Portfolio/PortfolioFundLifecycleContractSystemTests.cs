using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioFundLifecycleContractSystemTests
{
    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Fund_ui_contract_always_identifies_its_selected_Portfolio_parent()
    {
        var model = new FundMandateReadModel
        {
            PortfolioId = 101, FundId = 205, FundCode = "DAILY", Name = "Daily",
            FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
            EffectiveFromUtc = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc),
            DecisionHorizon = "Daily", Objective = "Directional", UnderlyingUniverse = ["ES"],
            EligibleAssetTypes = ["Futures"], PermittedTradeFamilies = ["DirectionalFuture"],
            CreatedOnUtc = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc), CreatedBy = "admin",
        };

        model.Validate().Should().BeEmpty();
        $"{model.PortfolioId}.{model.FundId}".Should().Be("101.205");
    }
}
