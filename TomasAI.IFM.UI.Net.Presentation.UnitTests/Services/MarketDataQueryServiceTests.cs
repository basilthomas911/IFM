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
    public async Task Value_date_comes_from_each_API_response_even_when_backtesting_moves_backwards()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var service = new MarketDataQueryService(api, Substitute.For<IMarketDataFeedQueryApi>());
        var future = new DateOnly(2035, 1, 3);
        var replay = new DateOnly(2008, 10, 10);
        api.GetValueDateAsync().Returns(
            new ServiceOk<ScalarReadModel<DateOnly>>(new(future)),
            new ServiceOk<ScalarReadModel<DateOnly>>(new(replay)));
        Assert.Equal(future, await service.GetValueDateAsync(CancellationToken.None));
        Assert.Equal(replay, await service.GetValueDateAsync(CancellationToken.None));
        await api.Received(2).GetValueDateAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_or_failed_API_date_never_falls_back_to_the_client_clock(bool failed)
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        api.GetValueDateAsync().Returns(failed
            ? new ServiceFailed<ScalarReadModel<DateOnly>>(503, "API unavailable")
            : new ServiceOk<ScalarReadModel<DateOnly>>(new(default(DateOnly))));
        var service = new MarketDataQueryService(api, Substitute.For<IMarketDataFeedQueryApi>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetValueDateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Waiting_for_API_date_honors_caller_cancellation_without_inventing_a_date()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var reply = new TaskCompletionSource<ServiceResult<ScalarReadModel<DateOnly>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.GetValueDateAsync().Returns(reply.Task);
        var service = new MarketDataQueryService(api, Substitute.For<IMarketDataFeedQueryApi>());
        using var cancellation = new CancellationTokenSource();
        var pending = service.GetValueDateAsync(cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

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
