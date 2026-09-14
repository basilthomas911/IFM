using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
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
            new DevelopmentTradingPortfolioIdentityRecovery(
                Substitute.For<IPortfolioDbReadContext>(), Substitute.For<IPortfolioProjectionRebuilder>(),
                Substitute.For<ILogger<DevelopmentTradingPortfolioIdentityRecovery>>()),
            new IntrinsicTimeStrategyWorkflowOptions(), Substitute.For<ILogger<DevelopmentTradingPortfolioProvisioner>>());

        await sut.Invoking(x => x.EnsureAsync()).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Development host environment*");
        await market.DidNotReceiveWithAnyArgs().GetTradeStrategySymbolsAsync(default);
        await configuration.DidNotReceiveWithAnyArgs().GetTradeSelectionVersionAsync(default, default);
    }

    [Fact]
    public async Task Identity_recovery_returns_the_existing_financial_owner_without_replaying_history()
    {
        var database = Substitute.For<IPortfolioDbReadContext>();
        var rebuilder = Substitute.For<IPortfolioProjectionRebuilder>();
        var book = Book(1201);
        var portfolio = Portfolio(1201);
        database.ReadActiveBookByExecutionAccountAsync("Emulator", "IFM-EMULATOR-PAPER", Arg.Any<CancellationToken>())
            .Returns(book);
        database.GetPortfolioAsync(1201, Arg.Any<CancellationToken>()).Returns(portfolio);
        foreach (var fund in book.Funds)
            database.GetFundAsync(fund.FundId, Arg.Any<CancellationToken>())
                .Returns(new FundMandateReadModel { PortfolioId = 1201, FundId = fund.FundId });
        database.GetPolicyAsync(1001, null, Arg.Any<CancellationToken>())
            .Returns(new PortfolioFinancialPolicyReadModel { PortfolioId = 1201, PolicyId = 1001 });
        var sut = new DevelopmentTradingPortfolioIdentityRecovery(
            database, rebuilder, Substitute.For<ILogger<DevelopmentTradingPortfolioIdentityRecovery>>());

        var result = await sut.ResolveAsync("Emulator", "IFM-EMULATOR-PAPER");

        result.Should().BeSameAs(portfolio);
        await rebuilder.DidNotReceiveWithAnyArgs().RebuildAsync(default!, default);
    }

    [Fact]
    public async Task Identity_recovery_rebuilds_only_the_authoritative_owner_streams_when_its_projection_is_missing()
    {
        var database = Substitute.For<IPortfolioDbReadContext>();
        var rebuilder = Substitute.For<IPortfolioProjectionRebuilder>();
        var book = Book(1201);
        var portfolio = Portfolio(1201);
        database.ReadActiveBookByExecutionAccountAsync("Emulator", "IFM-EMULATOR-PAPER", Arg.Any<CancellationToken>())
            .Returns(book);
        database.GetPortfolioAsync(1201, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PortfolioReadModel?>(null), Task.FromResult<PortfolioReadModel?>(portfolio));
        foreach (var fund in book.Funds)
            database.GetFundAsync(fund.FundId, Arg.Any<CancellationToken>())
                .Returns(new FundMandateReadModel { PortfolioId = 1201, FundId = fund.FundId });
        database.GetPolicyAsync(1001, null, Arg.Any<CancellationToken>())
            .Returns(new PortfolioFinancialPolicyReadModel { PortfolioId = 1201, PolicyId = 1001 });
        rebuilder.RebuildAsync(Arg.Any<PortfolioProjectionRebuildRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PortfolioProjectionRebuildResult(27, 9001, new('a', 64)));
        var sut = new DevelopmentTradingPortfolioIdentityRecovery(
            database, rebuilder, Substitute.For<ILogger<DevelopmentTradingPortfolioIdentityRecovery>>());

        var result = await sut.ResolveAsync("Emulator", "IFM-EMULATOR-PAPER");

        result.Should().BeSameAs(portfolio);
        await rebuilder.Received(1).RebuildAsync(
            Arg.Is<PortfolioProjectionRebuildRequest>(request =>
                request.Portfolios.SequenceEqual(new[] { new PortfolioId(1201) })
                && request.Funds.Select(x => x.Format()).SequenceEqual(new[] { "1201.5401", "1201.5501", "1201.5502" })
                && request.Policies!.SequenceEqual(new[] { new PortfolioFinancialPolicyId(1201, 1001) })),
            Arg.Any<CancellationToken>());
    }

    static FinancialBookConfiguration Book(int portfolioId) => new()
    {
        BookId = 1,
        PortfolioId = portfolioId,
        Environment = "Emulator",
        ExecutionAccountReference = "IFM-EMULATOR-PAPER",
        Funds =
        [
            Authority(5401),
            Authority(5501),
            Authority(5502),
        ],
    };

    static FinancialFundAuthority Authority(int fundId) => new()
    {
        FundId = fundId,
        Reference = new() { PolicyId = 1001 },
    };

    static PortfolioReadModel Portfolio(int portfolioId) => new()
    {
        PortfolioId = portfolioId,
        Name = "IFM Development Paper Portfolio",
        BaseCurrency = "USD",
        BrokerAccountRefs = ["IFM-EMULATOR-PAPER"],
    };
}

static class DevelopmentPolicyTestExtensions
{
    public static TimeFrameType TargetHorizon(this TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.SelectionConstructionPolicy policy) =>
        policy.ParameterSetId == DevelopmentTradingPortfolioDefaults.ConstructionId(TimeFrameType.Daily) ? TimeFrameType.Daily :
        policy.ParameterSetId == DevelopmentTradingPortfolioDefaults.ConstructionId(TimeFrameType.Weekly) ? TimeFrameType.Weekly : TimeFrameType.Monthly;
}
