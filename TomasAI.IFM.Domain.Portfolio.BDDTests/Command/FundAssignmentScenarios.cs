using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Command;

public sealed class FundAssignmentScenarios
{
    [Fact]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Given_a_Daily_Fund_when_a_directional_future_is_assigned_then_all_definition_versions_are_frozen()
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), new FundMandateReadModel
        {
            PortfolioId = 101, FundId = 205, FundCode = "DAILY", Name = "Daily", FundMandateVersion = 1,
            TradingYear = 2026, OperatingState = FundOperatingState.Draft, EffectiveFromUtc = now,
            DecisionHorizon = "Daily", Objective = "Directional", UnderlyingUniverse = ["ES"],
            EligibleAssetTypes = ["Futures"], PermittedTradeFamilies = ["DirectionalFuture"], CreatedOnUtc = now, CreatedBy = "admin",
        }, now, "admin");
        var assignment = new FundTradeTemplateAssignmentReadModel
        {
            PortfolioId = 101, PortfolioVersion = 1, FundId = 205, FundMandateVersion = 1, AssignmentVersion = 1,
            TradeTemplateId = Guid.NewGuid(), TradeTemplateVersion = 3, Enabled = true, DecisionHorizon = "Daily",
            UnderlyingUniverse = ["ES"], AssetType = "Futures", TradeFamily = "DirectionalFuture", Priority = 1,
            EffectiveFromUtc = now, TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 2,
            OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 4, CreatedOnUtc = now, CreatedBy = "admin",
        };

        aggregate.AssignTradeTemplate(Guid.NewGuid(), 1, assignment, now, "admin");

        aggregate.Assignments.Single().Should().BeEquivalentTo(assignment);
    }
}
