using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public sealed class MarketDataQueryModelTests
{
    [Fact]
    public async Task GetCurrentlyTradedFuturesContractsAsync_LoadsBothDashboardSymbols()
    {
        var queryApi = Substitute.For<IMarketDataQueryApi>();
        var feedApi = Substitute.For<IMarketDataFeedQueryApi>();
        var es = Contract("ES20260918", "ES");
        var vx = Contract("VX20260819", "VX");
        queryApi.GetCurrentlyTradedFuturesContractsAsync("ES")
            .Returns(new ServiceOk<FuturesContractV2ReadModel[]>([es]));
        queryApi.GetCurrentlyTradedFuturesContractsAsync("VX")
            .Returns(new ServiceOk<FuturesContractV2ReadModel[]>([vx]));
        var model = new MarketDataQueryModel(queryApi, feedApi);
        ICollection<FuturesContractV2ReadModel>? published = null;

        await model.GetCurrentlyTradedFuturesContractsAsync(values => published = values);

        published.Should().NotBeNull();
        published!.Select(contract => contract.Symbol).Should().Equal("ES", "VX");
        await queryApi.Received(1).GetCurrentlyTradedFuturesContractsAsync("ES");
        await queryApi.Received(1).GetCurrentlyTradedFuturesContractsAsync("VX");
    }

    static FuturesContractV2ReadModel Contract(string contractId, string symbol)
        => new(
            contractId,
            $"{contractId} contract",
            symbol,
            contractId,
            "FUT",
            "USD",
            "CME",
            symbol == "VX" ? "1000" : "50",
            new DateOnly(2026, 9, 18),
            true);
}
