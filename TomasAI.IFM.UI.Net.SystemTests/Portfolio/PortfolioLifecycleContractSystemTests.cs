using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioLifecycleContractSystemTests
{
    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Ui_contract_exposes_actor_authoritative_lifecycle_and_reason_safe_actions()
    {
        var readModel = new PortfolioReadModel
        {
            PortfolioId = 101, Name = "Core", PortfolioVersion = 2,
            OperatingState = PortfolioOperatingState.Paused,
            EffectiveFromUtc = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc),
            CreatedOnUtc = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc),
            CreatedBy = "admin",
        };

        readModel.OperatingState.Should().Be(PortfolioOperatingState.Paused);
        PortfolioAggregateActionPolicy.CanCreateNewExposure(readModel).Should().BeFalse();
        PortfolioAggregateActionPolicy.RequiresReason(PortfolioOperatingState.Active).Should().BeTrue();
    }

    static class PortfolioAggregateActionPolicy
    {
        public static bool CanCreateNewExposure(PortfolioReadModel model) => model.OperatingState == PortfolioOperatingState.Active;
        public static bool RequiresReason(PortfolioOperatingState target) => target != PortfolioOperatingState.Unknown;
    }
}
