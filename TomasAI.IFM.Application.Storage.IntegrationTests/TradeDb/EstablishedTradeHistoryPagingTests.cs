using TomasAI.IFM.Domain.Trade.Shared;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

/// <summary>Exercises bounded native PostgreSQL trade history pages, including tied ordering keys.</summary>
public sealed class EstablishedTradeHistoryPagingTests(TradeDbFixture fixture) : IClassFixture<TradeDbFixture>
{
    [Fact]
    public async Task Pages_preserve_every_revision_with_tied_dates_order_and_trade_ids_without_duplicates()
    {
        var fund = Random.Shared.Next(10000, 900000);
        var at = new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);
        EstablishedTradeDefinition Row(int order, int trade, int revision, DateTime time) => new()
        {
            Id = new(11, fund, order, trade), StrategyKind = TradeStrategyKind.FuturesOutright,
            AssetFamily = TradeAssetFamily.Futures, Status = EstablishedTradeStatus.Open,
            EstablishedAtUtc = time, EvidenceRevision = revision
        };
        var rows = new[] { Row(10,20,3,at), Row(10,20,2,at), Row(10,20,1,at), Row(10,21,1,at), Row(11,10,1,at), Row(2,2,1,at.AddMinutes(-1)) };
        foreach (var row in rows.Reverse()) await fixture.TradeDb.UpsertEstablishedTradeAsync(row);
        await fixture.TradeDb.UpsertEstablishedTradeAsync(rows[0] with { Id = new(11, fund + 1, 10, 20) });
        var received = new List<EstablishedTradeDefinition>();
        byte[]? cursor = null;
        do
        {
            var page = await fixture.TradeDb.GetEstablishedTradesAsync(11, fund, TradeStrategyKind.FuturesOutright,
                at.AddMinutes(-2), at.AddMinutes(1), 2, cursor);
            Assert.InRange(page.Items.Length, 1, 2);
            received.AddRange(page.Items);
            cursor = page.PagingState;
        } while (cursor is not null);
        Assert.Equal(rows.Select(row => (row.Id, row.EvidenceRevision)), received.Select(row => (row.Id, row.EvidenceRevision)));
        Assert.Equal(rows.Length, received.Select(row => (row.Id, row.EvidenceRevision)).Distinct().Count());
    }

    [Fact]
    public async Task Cursor_rejects_different_fund_kind_date_scope_and_malformed_content()
    {
        var fund = Random.Shared.Next(10000, 900000);
        var at = new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);
        var row = new EstablishedTradeDefinition { Id = new(12, fund, 10, 20), StrategyKind = TradeStrategyKind.FuturesOutright,
            AssetFamily = TradeAssetFamily.Futures, Status = EstablishedTradeStatus.Open, EstablishedAtUtc = at, EvidenceRevision = 1 };
        await fixture.TradeDb.UpsertEstablishedTradeAsync(row);
        await fixture.TradeDb.UpsertEstablishedTradeAsync(row with { Id = new(12, fund, 10, 21) });
        var from = at.AddMinutes(-1); var through = at.AddMinutes(1);
        var first = await fixture.TradeDb.GetEstablishedTradesAsync(12, fund, row.StrategyKind, from, through, 1);
        Assert.NotNull(first.PagingState);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.TradeDb.GetEstablishedTradesAsync(12, fund + 1, row.StrategyKind, from, through, 1, first.PagingState));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.TradeDb.GetEstablishedTradesAsync(12, fund, TradeStrategyKind.IronCondor, from, through, 1, first.PagingState));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.TradeDb.GetEstablishedTradesAsync(12, fund, row.StrategyKind, from.AddSeconds(-1), through, 1, first.PagingState));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.TradeDb.GetEstablishedTradesAsync(12, fund, row.StrategyKind, from, through, 1, [0xff]));
    }
}
