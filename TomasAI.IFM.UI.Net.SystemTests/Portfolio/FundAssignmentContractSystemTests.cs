using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class FundAssignmentContractSystemTests
{
    [Fact]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Assignment_ui_contract_exposes_definition_versions_and_enabled_window()
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var model = new FundTradeTemplateAssignmentReadModel { PortfolioId=101, PortfolioVersion=1, FundId=205, FundMandateVersion=1, AssignmentVersion=1, TradeTemplateId=Guid.NewGuid(), TradeTemplateVersion=3, Enabled=true, DecisionHorizon="Daily", UnderlyingUniverse=["ES"], AssetType="Futures", TradeFamily="DirectionalFuture", Priority=1, EffectiveFromUtc=now, TradeSelectionHintProfileId=Guid.NewGuid(), TradeSelectionHintProfileVersion=2, OrderCompositionProfileId=Guid.NewGuid(), OrderCompositionProfileVersion=4, CreatedOnUtc=now, CreatedBy="admin" };
        model.IsEffectiveAt(now.AddHours(1)).Should().BeTrue();
        model.TradeTemplateVersion.Should().Be(3);
        model.TradeSelectionHintProfileVersion.Should().Be(2);
        model.OrderCompositionProfileVersion.Should().Be(4);
    }
}
