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
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Symbol_lookup_preserves_family_cancellation_and_provider_result(bool success)
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var service = new MarketDataQueryService(api, Substitute.For<IMarketDataFeedQueryApi>());
        using var cancellation = new CancellationTokenSource();
        var family = TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType.FuturesOption;
        ServiceResult<TradeStrategySymbolReadModel[]> expected = success
            ? new ServiceOk<TradeStrategySymbolReadModel[]>([new() { Id = 101, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures options" }])
            : new ServiceFailed<TradeStrategySymbolReadModel[]>(503, "metadata unavailable");
        api.GetTradeStrategySymbolsAsync(family, cancellation.Token).Returns(expected);
        (await service.GetTradeStrategySymbolsAsync(family, cancellation.Token)).Should().BeSameAs(expected);
        await api.Received(1).GetTradeStrategySymbolsAsync(family, cancellation.Token);
    }
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
