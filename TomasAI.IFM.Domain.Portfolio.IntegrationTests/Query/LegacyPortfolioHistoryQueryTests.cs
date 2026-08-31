using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Query;

public sealed class LegacyPortfolioHistoryQueryTests
{
    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public async Task Query_joins_composition_to_hydrated_TradeDb_trade_without_any_write_dependency()
    {
        var store = Substitute.For<ILegacyPortfolioHistoryStore>();
        var composition = new FundOrderTradeReadModel
        {
            FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            TradeDate = new DateOnly(2024, 1, 2), MaturityDate = new DateOnly(2024, 2, 2),
        };
        var trade = new OptionTradeReadModel { OrderId = 1084, TradeId = 1090 }
            .AddTradePosition([new TradePositionReadModel { OrderId = 1084, TradeId = 1090, ValueDate = new DateOnly(2024, 1, 2) }]);
        store.GetCompositionTradesAsync(Arg.Any<CancellationToken>()).Returns([composition]);
        store.GetTradeDbTradeAsync(1084, 1090, Arg.Any<CancellationToken>()).Returns(trade);
        var service = new LegacyPortfolioHistoryQueryService(store,
            Substitute.For<IPortfolioDbReadContext>(), Substitute.For<IPortfolioBusinessIdHighWatermark>());

        var result = await service.GetOrderTradesAsync(1004, 1084);

        result.Success.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.MatchStatus.Should().Be(LegacyTradeMatchStatus.PositionHistory);
        result.Value![0].TradeDbTrade.Should().BeSameAs(trade);
        await store.Received(1).GetTradeDbTradeAsync(1084, 1090, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public async Task Catalog_retains_orphan_FundIds_as_separately_queryable_unassigned_history()
    {
        var store = Substitute.For<ILegacyPortfolioHistoryStore>();
        store.GetFundsAsync(Arg.Any<CancellationToken>()).Returns([
            new FundReadModel(1004, "Legacy", "history", 0m, false, DateTime.UtcNow, "test")]);
        store.GetOrdersAsync(Arg.Any<CancellationToken>()).Returns([
            new FundOrderReadModel(1003, 1, DateTime.UtcNow, Domain.Fund.Shared.OrderStatus.Open, "ES", new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 2), "unassigned", DateTime.UtcNow, "test", null, string.Empty),
            new FundOrderReadModel(1016, 2, DateTime.UtcNow, Domain.Fund.Shared.OrderStatus.Open, "ES", new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 2), "unassigned", DateTime.UtcNow, "test", null, string.Empty)]);
        store.GetCompositionTradesAsync(Arg.Any<CancellationToken>()).Returns([
            new FundOrderTradeReadModel { FundId = 1003, OrderId = 1, TradeId = 1 }]);
        var service = new LegacyPortfolioHistoryQueryService(store,
            Substitute.For<IPortfolioDbReadContext>(), Substitute.For<IPortfolioBusinessIdHighWatermark>());

        var result = await service.GetCatalogAsync();

        result.Value.Should().ContainSingle(x => x.IsUnassigned && x.Fund.FundId == 1003 && x.OrderCount == 1 && x.CompositionTradeCount == 1);
        result.Value.Should().ContainSingle(x => x.IsUnassigned && x.Fund.FundId == 1016 && x.OrderCount == 1 && x.CompositionTradeCount == 0);
    }
}
