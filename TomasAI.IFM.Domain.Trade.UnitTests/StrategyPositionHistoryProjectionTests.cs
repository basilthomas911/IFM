using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public class StrategyPositionHistoryProjectionTests
{
    [Fact]
    public void Backend_opening_history_uses_weighted_fills_and_exchange_value_date()
    {
        var shortId = Guid.NewGuid(); var longId = Guid.NewGuid();
        var trade = new EstablishedTradeDefinition { Id = new(1,2,3,4), StrategyKind = TradeStrategyKind.VerticalSpread,
            EstablishedAtUtc = new(2026,10,6,23,0,0,DateTimeKind.Utc), OpeningValue = -6m, OpeningCommission = 2.6m,
            Legs = [new() { TradeLegId=shortId,ContractId="short",SignedQuantity=-2,Expiry=new(2026,11,20) },
                new() { TradeLegId=longId,ContractId="long",SignedQuantity=2,Expiry=new(2026,11,20) }],
            OriginalFills = [new() { TradeLegId=shortId,SignedQuantity=-1,Price=28m },
                new() { TradeLegId=shortId,SignedQuantity=-1,Price=32m },new() { TradeLegId=longId,SignedQuantity=2,Price=24m }] };
        var result = StrategyPositionHistoryProjection.EnrichTrade(trade);
        result.OpeningPositionSnapshot!.Legs[0].OpeningPrice.Should().Be(30m);
        result.OpeningPositionSnapshot.Legs[1].OpeningPrice.Should().Be(24m);
        result.OpeningHistoryValuation!.ValueDate.Should().Be(new DateOnly(2026,10,7));
        result.OpeningHistoryValuation.DaysToExpiry.Should().Be(44);
        result.OpeningHistoryValuation.NetPrice.Should().Be(-6m);
        result.OpeningHistoryValuation.Commission.Should().Be(2.6m);
        var mark = result.OpeningPositionSnapshot with { Phase = StrategyPositionPhase.MarkToMarket,
            Legs = [result.OpeningPositionSnapshot.Legs[0] with { CurrentPrice=34m }, result.OpeningPositionSnapshot.Legs[1] with { CurrentPrice=25m }] };
        StrategyPositionHistoryProjection.EnrichPosition(mark, trade).HistoryValuation!.NetPrice.Should().Be(-9m);
    }

    [Fact]
    public void Missing_fill_evidence_does_not_fabricate_an_opening_position()
    {
        var trade = new EstablishedTradeDefinition { Id=new(1,2,3,4), EstablishedAtUtc=DateTime.UtcNow,
            Legs=[new() { TradeLegId=Guid.NewGuid(), ContractId="missing",Expiry=new(2026,12,18) }] };
        StrategyPositionHistoryProjection.EnrichTrade(trade).OpeningPositionSnapshot.Should().BeNull();
    }
}
