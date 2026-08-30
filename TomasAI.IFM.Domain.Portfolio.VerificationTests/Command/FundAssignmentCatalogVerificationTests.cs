using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Command;

public sealed class FundAssignmentCatalogVerificationTests
{
    [Theory]
    [InlineData("Daily", "Futures", "DirectionalFuture")]
    [InlineData("Weekly", "FuturesOptions", "VerticalSpread")]
    [InlineData("Monthly", "FuturesOptions", "DirectionalIronCondor")]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Initial_catalog_assignments_are_intrinsically_valid(string horizon, string asset, string family)
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        new FundTradeTemplateAssignmentReadModel { PortfolioId=101, PortfolioVersion=1, FundId=205, FundMandateVersion=1, AssignmentVersion=1, TradeTemplateId=Guid.NewGuid(), TradeTemplateVersion=1, Enabled=true, DecisionHorizon=horizon, UnderlyingUniverse=["ES"], AssetType=asset, TradeFamily=family, Priority=1, EffectiveFromUtc=now, TradeSelectionHintProfileId=Guid.NewGuid(), TradeSelectionHintProfileVersion=1, OrderCompositionProfileId=Guid.NewGuid(), OrderCompositionProfileVersion=1, CreatedOnUtc=now, CreatedBy="verification" }.Validate().Should().BeEmpty();
    }
}
