using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Development;

public sealed class DevelopmentTradingPortfolioTests
{
    [Fact]
    public void Defaults_are_complete_stable_and_valid_for_all_three_horizons()
    {
        var first = DevelopmentTradingPortfolioDefaults.ConstructionPolicies();
        var second = DevelopmentTradingPortfolioDefaults.ConstructionPolicies();

        first.Select(x => x.ParameterSetId).Should().Equal(second.Select(x => x.ParameterSetId));
        first.Select(x => x.TargetHorizon()).Should().Equal(
            TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly);
        first.Should().OnlyContain(x => x.Version == 1 && x.SchemaVersion == 1);
        foreach (var policy in first) policy.Invoking(x => x.Validate()).Should().NotThrow();
        DevelopmentTradingPortfolioDefaults.ActivationId(2026, TimeFrameType.Daily)
            .Should().NotBe(DevelopmentTradingPortfolioDefaults.ActivationId(2027, TimeFrameType.Daily));
    }

    [Theory]
    [InlineData(1000000, 1000000)]
    [InlineData(100, 100)]
    [InlineData(1, 1)]
    public void Capital_allocation_preserves_the_exact_separately_entered_total(decimal capital, decimal expected)
    {
        var allocations = DevelopmentTradingPortfolioDefaults.CapitalAllocations(capital);
        allocations.Should().HaveCount(3);
        allocations.Sum().Should().Be(expected);
        allocations.Should().OnlyContain(x => x > 0);
    }

    [Fact]
    public async Task Provisioner_refuses_non_development_host_before_any_external_access()
    {
        var configuration = Substitute.For<IConfigurationDbContext>();
        var market = Substitute.For<IMarketDataApi>();
        var sut = new DevelopmentTradingPortfolioProvisioner(
            new DevelopmentTradingPortfolioOptions { Enabled = true }, new FinancialDevelopmentPolicy(false), configuration,
            market, Substitute.For<IPortfolioBusinessIdAllocator>(), Substitute.For<IPortfolioCommandApi>(),
            Substitute.For<IPortfolioFundCommandApi>(), Substitute.For<IPortfolioFinancialPolicyCommandApi>(),
            Substitute.For<IPortfolioQueryApi>(), Substitute.For<IPortfolioFinancialApi>(),
            new IntrinsicTimeStrategyWorkflowOptions(), Substitute.For<ILogger<DevelopmentTradingPortfolioProvisioner>>());

        await sut.Invoking(x => x.EnsureAsync()).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Development host environment*");
        await market.DidNotReceiveWithAnyArgs().GetTradeStrategySymbolsAsync(default);
        await configuration.DidNotReceiveWithAnyArgs().GetTradeSelectionVersionAsync(default, default);
    }
}

static class DevelopmentPolicyTestExtensions
{
    public static TimeFrameType TargetHorizon(this TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.SelectionConstructionPolicy policy) =>
        policy.ParameterSetId == DevelopmentTradingPortfolioDefaults.ConstructionId(TimeFrameType.Daily) ? TimeFrameType.Daily :
        policy.ParameterSetId == DevelopmentTradingPortfolioDefaults.ConstructionId(TimeFrameType.Weekly) ? TimeFrameType.Weekly : TimeFrameType.Monthly;
}
