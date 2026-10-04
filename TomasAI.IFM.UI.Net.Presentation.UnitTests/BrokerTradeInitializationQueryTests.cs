using NSubstitute;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests;

public sealed class BrokerTradeInitializationQueryTests
{
    [Fact]
    public async Task Combined_result_preserves_selection_and_successful_sources_when_another_source_fails()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        root.Services.Returns(services);
        var financial = Substitute.For<IPortfolioFinancialApi>();
        services.PortfolioFinancial.Returns(financial);
        services.PortfolioQueries.Returns(Substitute.For<IPortfolioQueryApi>());
        services.BrokerAccounts.Returns(Substitute.For<IBrokerAccountQueryApi>());
        var tradeApi = Substitute.For<ITradeQueryApi>();
        services.TradeQueries.Returns(new TradeQueryService(tradeApi));
        services.MarketDataQueries.Returns(new MarketDataQueryService(Substitute.For<IMarketDataQueryApi>(), Substitute.For<IMarketDataFeedQueryApi>()));
        var snapshot = new FinancialRead<FinancialBalanceSnapshot>(FinancialReadStatus.Found,
            new(1, "Open", [], 0, 12345m, true), 9, DateTime.UtcNow);
        financial.GetAccountBalancesAsync(Arg.Any<FinancialReadScope>(), Arg.Any<GetAccountBalancesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FinancialRead<FinancialBalanceSnapshot>>(snapshot));
        financial.GetCapacityUsageAsync(Arg.Any<FinancialReadScope>(), Arg.Any<GetCapacityUsageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceFailed<FinancialRead<FinancialCapacityUsage>>(503, "Storage unavailable"));
        tradeApi.GetOptionTradeAsync(1, 2).Returns(new ServiceFailed<OptionTradeReadModel>(404, "New draft has no stored option trade"));
        BrokerTradeLegData[] selection = [new("ES-option", "+LP", 5000, 10, 11, -.2, false, 12, 50)];
        var result = await BrokerTradeInitializationQuery.ExecuteAsync(root, 3, 4, 1, 2,
            "ES20261218", new(2026, 10, 3), selection, BrokerCapabilities.Emulator("test"));
        Assert.Equal(12345m, result.Balances!.Value!.AvailableCash);
        Assert.Equal(selection, result.SelectedLegs);
        Assert.Null(result.CapacityUsage);
        Assert.Contains(result.Errors, error => error.Contains("Storage unavailable"));
        Assert.DoesNotContain(result.Errors, error => error.Contains("New draft"));
        await financial.Received(1).GetAccountBalancesAsync(
            Arg.Is<FinancialReadScope>(scope => scope.PortfolioId == 3 && scope.FundId == 4),
            Arg.Any<GetAccountBalancesRequest>(), Arg.Any<CancellationToken>());
    }
}
