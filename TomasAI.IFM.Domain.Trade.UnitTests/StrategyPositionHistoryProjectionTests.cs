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
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void Closed_history_reports_currency_realized_pnl_and_all_commissions_using_captured_multiplier(decimal oldMultiplier)
    {
        var legId = Guid.NewGuid();
        var trade = new EstablishedTradeDefinition {
            Id = new(101,701,1901,1501), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = new(2026,10,16,14,0,0,DateTimeKind.Utc), OpeningCommission = 2.60m,
            Legs = [new() { TradeLegId=legId,ContractId="ES-option",SignedQuantity=-1,
                CashMultiplier=oldMultiplier,Expiry=new(2026,11,20) }] };
        var position = new StrategyPositionSnapshot {
            Id = StrategyPositionId.Create(trade.Id, trade.StrategyKind), StrategyKind=trade.StrategyKind,
            Phase=StrategyPositionPhase.Close, ValueDate=new(2026,11,6), AsOfUtc=new(2026,11,7,15,0,0,DateTimeKind.Utc),
            IsOpen=false, RealizedPnl=-6.95m, ContractCashMultiplier=50m,
            Legs=[new() { TradeLegId=legId,ContractId="ES-option",CashMultiplier=50m,SignedQuantity=0 }],
            ClosingFills=Enumerable.Range(0,4).Select(_=>new ExecutionFillEvidence {
                Commission=.65m,FilledAtUtc=new(2026,11,7,15,0,0,DateTimeKind.Utc) }).ToArray() };
        var enriched = StrategyPositionHistoryProjection.EnrichPosition(position, trade);
        enriched.RealizedPnl.Should().Be(-6.95m, "stored point evidence must remain unchanged");
        enriched.HistoryValuation!.RealizedPnl.Should().Be(-347.50m);
        enriched.HistoryValuation.Commission.Should().Be(2.60m);
        enriched.HistoryValuation.TotalCommission.Should().Be(5.20m);
        enriched.HistoryValuation.NetPnl.Should().Be(-352.70m);
        StrategyPositionHistoryProjection.EnrichPosition(enriched, trade).Should().BeEquivalentTo(enriched);
        var roundTrip = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared
            .Deserialize<StrategyPositionSnapshot>(TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(enriched));
        roundTrip.HistoryValuation.Should().Be(enriched.HistoryValuation);
    }

    [Fact]
    public void Missing_multiplier_does_not_label_raw_points_as_realized_currency()
    {
        var trade = new EstablishedTradeDefinition { Id=new(1,2,3,4),StrategyKind=TradeStrategyKind.IronCondor,
            EstablishedAtUtc=new(2026,10,16,14,0,0,DateTimeKind.Utc),OpeningCommission=2.60m,
            Legs=[new() { ContractId="ES-option",Expiry=new(2026,11,20) }] };
        var result = StrategyPositionHistoryProjection.EnrichPosition(new() {
            Id=StrategyPositionId.Create(trade.Id,trade.StrategyKind),StrategyKind=trade.StrategyKind,
            ValueDate=new(2026,11,6),RealizedPnl=-6.95m },trade);
        result.HistoryValuation!.RealizedPnl.Should().BeNull();
        result.HistoryValuation.NetPnl.Should().BeNull();
    }

    [Fact]
    public void Complete_history_deltas_sum_to_net_without_repeating_opening_cost_or_eod_on_close()
    {
        var leg=Guid.NewGuid(); var day=new DateOnly(2026,10,16);
        var trade=new EstablishedTradeDefinition { Id=new(1,2,3,4),StrategyKind=TradeStrategyKind.IronCondor,
            EstablishedAtUtc=new(2026,10,16,14,0,0,DateTimeKind.Utc),OpeningCommission=2.60m,
            Legs=[new() { TradeLegId=leg,ContractId="ES",SignedQuantity=1,CashMultiplier=50,Expiry=new(2026,11,20) }],
            OriginalFills=[new() { TradeLegId=leg,SignedQuantity=1,Price=10 }] };
        var open=StrategyPositionHistoryProjection.EnrichTrade(trade).OpeningPositionSnapshot!;
        var first=open with { Phase=StrategyPositionPhase.EndOfDay,ValueDate=day,AsOfUtc=open.AsOfUtc.AddHours(7),UnrealizedPnl=.60m };
        var final=first with { ValueDate=new(2026,11,6),AsOfUtc=new(2026,11,6,22,0,0,DateTimeKind.Utc),UnrealizedPnl=-5.05m };
        var close=final with { Phase=StrategyPositionPhase.Close,IsOpen=false,AsOfUtc=final.AsOfUtc.AddHours(17),
            UnrealizedPnl=0,RealizedPnl=-6.95m,ClosingFills=[new() { Commission=2.60m,FilledAtUtc=final.AsOfUtc.AddHours(17) }] };
        var rows=StrategyPositionHistoryProjection.EnrichHistory([open,first,final,close],trade);
        rows.Select(p=>p.HistoryValuation!.PeriodPnl).Should().Equal(-2.60m,30m,-282.50m,-97.60m);
        rows.Sum(p=>p.HistoryValuation!.PeriodPnl).Should().Be(-352.70m);
        rows.Last().HistoryValuation!.NetPnl.Should().Be(-352.70m);
    }

}
