using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class FundAssignmentTests
{
    static readonly DateTime Now = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Assignments_are_ordered_and_same_template_windows_cannot_overlap()
    {
        var aggregate = CreateFund();
        var first = Assignment(Guid.NewGuid(), 1, 20);
        var second = Assignment(Guid.NewGuid(), 2, 10);
        aggregate.AssignTradeTemplate(Guid.NewGuid(), 1, first, Now, "admin");
        aggregate.AssignTradeTemplate(Guid.NewGuid(), 2, second, Now, "admin");

        aggregate.EffectiveAssignments(Now.AddDays(1)).Select(x => x.Priority).Should().Equal(10, 20);

        var overlap = Assignment(first.TradeTemplateId, 3, 30);
        var action = () => aggregate.AssignTradeTemplate(Guid.NewGuid(), 3, overlap, Now, "admin");
        action.Should().Throw<InvalidOperationException>().WithMessage("*overlapping*");
    }

    [Fact]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Incompatible_asset_or_horizon_is_rejected()
    {
        var aggregate = CreateFund();
        var action = () => aggregate.AssignTradeTemplate(Guid.NewGuid(), 1,
            Assignment(Guid.NewGuid(), 1, 1) with { AssetType = "FuturesOptions" }, Now, "admin");
        action.Should().Throw<ArgumentException>().WithMessage("*asset type*");
    }

    internal static PortfolioFundAggregate CreateFund()
    {
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), new FundMandateReadModel
        {
            PortfolioId = 101, FundId = 205, FundCode = "DAILY", Name = "Daily",
            FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
            EffectiveFromUtc = Now, DecisionHorizon = "Daily", Objective = "Directional",
            UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"],
            PermittedTradeFamilies = ["DirectionalFuture"], CreatedOnUtc = Now, CreatedBy = "admin",
        }, Now, "admin");
        return aggregate;
    }

    internal static FundTradeTemplateAssignmentReadModel Assignment(Guid templateId, long version, int priority) => new()
    {
        PortfolioId = 101, PortfolioVersion = 1, FundId = 205, FundMandateVersion = 1,
        AssignmentVersion = version, TradeTemplateId = templateId, TradeTemplateVersion = 1,
        Enabled = true, DecisionHorizon = "Daily", UnderlyingUniverse = ["ES"],
        AssetType = "Futures", TradeFamily = "DirectionalFuture", Priority = priority,
        EffectiveFromUtc = Now, TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1,
        OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1,
        CreatedOnUtc = Now, CreatedBy = "admin",
    };
}
