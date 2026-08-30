using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Command;

public sealed class PortfolioFundCatalogVerificationTests
{
    [Theory]
    [InlineData(205, "Daily", "Futures", "DirectionalFuture")]
    [InlineData(206, "Weekly", "FuturesOptions", "VerticalSpread")]
    [InlineData(207, "Monthly", "FuturesOptions", "DirectionalIronCondor")]
    [Trait("Gate", "PF-04")]
    [Trait("Category", "Portfolio")]
    public void Minimum_mandates_retain_all_required_classification_inputs(int fundId, string horizon, string asset, string family)
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), new FundMandateReadModel
        {
            PortfolioId = 101, FundId = fundId, FundCode = horizon.ToUpperInvariant(), Name = horizon,
            FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
            EffectiveFromUtc = now, DecisionHorizon = horizon, Objective = "ES strategy",
            UnderlyingUniverse = ["ES"], EligibleAssetTypes = [asset], PermittedTradeFamilies = [family],
            CreatedOnUtc = now, CreatedBy = "verification",
        }, now, "verification");

        aggregate.Current!.DecisionHorizon.Should().Be(horizon);
        aggregate.Current.EligibleAssetTypes.Should().ContainSingle(asset);
        aggregate.Current.PermittedTradeFamilies.Should().ContainSingle(family);
    }
}
