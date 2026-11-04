using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Futures.Option;

public sealed class FuturesOptionTradeTypedReadTests
{
    [Fact]
    public async Task History_uses_the_typed_storage_read_and_preserves_date_and_status_order()
    {
        var database = Substitute.For<ITradeDbContext>();
        var factory = Substitute.For<IDbContextFactory>(); factory.TradeDb.Returns(database);
        using var lifetime = new CancellationTokenSource();
        var day = new DateOnly(2026, 11, 3);
        TradeHistoryReadModel Row(DateOnly date, TradeStatus status) => new(orderId: 1, tradeId: 2,
            tradeType: TradeType.ShortIronCondor, valueDate: date, daysToExpiry: 3, tradeStatus: status,
            commission: 0, netSpread: 0, tradePnl: 0);
        var open = Row(day, TradeStatus.Open); var close = Row(day, TradeStatus.Close); var next = Row(day.AddDays(1), TradeStatus.Open);
        database.GetTradeHistoryAsync(1, lifetime.Token).Returns([next, close, Row(day, (TradeStatus)999), open]);
        Assert.Equal(new[] { open, close, next }, await factory.GetTradeHistoryAsync(1, lifetime.Token));
        await database.Received(1).GetTradeHistoryAsync(1, lifetime.Token);
    }

    [Fact]
    public async Task Quantity_uses_the_typed_storage_read_with_its_cancellation_token()
    {
        var database = Substitute.For<ITradeDbContext>();
        var factory = Substitute.For<IDbContextFactory>(); factory.TradeDb.Returns(database);
        using var lifetime = new CancellationTokenSource();
        database.GetTradeQuantityAsync(12, lifetime.Token).Returns(4);
        var result = await factory.GetTradeQuantityAsync(12, lifetime.Token);
        Assert.Equal(4, result.Value);
        await database.Received(1).GetTradeQuantityAsync(12, lifetime.Token);
    }
}
