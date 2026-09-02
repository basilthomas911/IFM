using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

/// <summary>Verifies Market Data query service mapping and failure behavior.</summary>
public sealed class MarketDataQueryServiceTests
{
    [Fact]
    public async Task GetRolloverFuturesContractsAsync_LoadsBothDashboardSymbols()
    {
        var queryApi = Substitute.For<IMarketDataQueryApi>();
        var feedApi = Substitute.For<IMarketDataFeedQueryApi>();
        var es = Contract("ES20260918", "ES");
        var vx = Contract("VX20260819", "VX");
        queryApi.GetRolloverFuturesContractsAsync("ES")
            .Returns(new ServiceOk<FuturesContractV3ReadModel[]>([es]));
        queryApi.GetRolloverFuturesContractsAsync("VX")
            .Returns(new ServiceOk<FuturesContractV3ReadModel[]>([vx]));
        var model = new MarketDataQueryService(queryApi, feedApi);
        ICollection<FuturesContractV3ReadModel>? published = null;

        await model.GetRolloverFuturesContractsAsync(values => published = values);

        published.Should().NotBeNull();
        published!.Select(contract => contract.Symbol).Should().Equal("ES", "VX");
        await queryApi.Received(1).GetRolloverFuturesContractsAsync("ES");
        await queryApi.Received(1).GetRolloverFuturesContractsAsync("VX");
    }

    static FuturesContractV3ReadModel Contract(string contractId, string symbol)
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
