using TomasAI.IFM.Domain.Trade.Futures.Position.Command;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class StoredQuotePositionEndOfDayRecoveryTests
{
    [Theory]
    [InlineData(TradeStrategyKind.IronCondor)]
    [InlineData(TradeStrategyKind.VerticalSpread)]
    [InlineData(TradeStrategyKind.FuturesOutright)]
    public void Typed_repair_commands_preserve_today_observations_and_emit_the_historical_seal(TradeStrategyKind kind)
    {
        var date=new DateOnly(2026,11,17);var boundary=FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime;
        var original=new StrategyPositionSnapshot {Id=StrategyPositionId.Create(new(101,701,1,1),kind),StrategyKind=kind,
            ValueDate=date,Phase=StrategyPositionPhase.EndOfDay,IsOpen=true,PositionSequence=1,LatestSealedValueDate=date,AsOfUtc=boundary,
            HistoryAsOfUtc=boundary,HistoryPositionSequence=1,Legs=[new() {TradeLegId=Guid.NewGuid(),ContractId="TEST",SignedQuantity=1,OpeningPrice=9,CurrentPrice=9,LastPriceAtUtc=boundary.AddHours(-1)}]};
        var current=original with {PositionSequence=2,ValueDate=date.AddDays(1),Phase=StrategyPositionPhase.MarkToMarket,AsOfUtc=boundary.AddHours(2),HistoryAsOfUtc=boundary.AddHours(2),HistoryPositionSequence=2};
        var quote=new StoredPositionClosingQuote {EvidenceId=new string('A',64),SourceArchiveSha256=new string('B',64),ContractId="TEST",InstrumentId=1,SourceSequence=1,ValueDate=date,
            EventTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,ReceiveTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,Bid=10,Ask=12,BidSize=1,AskSize=1};
        StrategyPositionSnapshot? saved=null;StrategyPositionSnapshot? seal=null;
        if(kind==TradeStrategyKind.IronCondor) {
            var command=new EndOfDayIronCondorPositionCommand {CommandId=Guid.NewGuid(),EntityId=original.Id,HistoricalSnapshot=original,HistoricalSourceEventId=123,HistoricalClosingQuotes=[quote],ValueDate=date,EffectiveAtUtc=boundary};
            var state=new IronCondorPositionCommandState();state.Update(new IronCondorPositionChangedEvent {EntityId=original.Id,PositionSnapshot=current},command);state.AcceptChanges();
            Assert.True(command.Execute(state).Success);var fact=Assert.Single(state.Events.OfType<IronCondorPositionChangedEvent>());saved=fact.PositionSnapshot;seal=fact.HistoricalEndOfDay;
        } else if(kind==TradeStrategyKind.VerticalSpread) {
            var command=new EndOfDayVerticalSpreadPositionCommand {CommandId=Guid.NewGuid(),EntityId=original.Id,HistoricalSnapshot=original,HistoricalSourceEventId=123,HistoricalClosingQuotes=[quote],ValueDate=date,EffectiveAtUtc=boundary};
            var state=new VerticalSpreadPositionCommandState();state.Update(new VerticalSpreadPositionChangedEvent {EntityId=original.Id,PositionSnapshot=current},command);state.AcceptChanges();
            Assert.True(command.Execute(state).Success);var fact=Assert.Single(state.Events.OfType<VerticalSpreadPositionChangedEvent>());saved=fact.PositionSnapshot;seal=fact.HistoricalEndOfDay;
        } else {
            var command=new EndOfDayFuturesPositionCommand {CommandId=Guid.NewGuid(),EntityId=original.Id,HistoricalSnapshot=original,HistoricalSourceEventId=123,HistoricalClosingQuotes=[quote],ValueDate=date,EffectiveAtUtc=boundary};
            var state=new FuturesPositionCommandState();state.Update(new FuturesPositionChangedEvent {EntityId=original.Id,PositionSnapshot=current},command);state.AcceptChanges();
            Assert.True(command.Execute(state).Success);var fact=Assert.Single(state.Events.OfType<FuturesPositionChangedEvent>());saved=fact.PositionSnapshot;seal=fact.HistoricalEndOfDay;
        }
        Assert.NotNull(saved);Assert.NotNull(seal);Assert.Equal(current.AsOfUtc,saved.AsOfUtc);Assert.Equal(current.Legs[0].LastPriceAtUtc,saved.Legs[0].LastPriceAtUtc);
        Assert.Equal(2,saved.DailyOpeningPnl);Assert.Equal(11,seal.Legs[0].CurrentPrice);Assert.Equal(quote.ObservedAtUtc,seal.Legs[0].LastPriceAtUtc);
    }

    [Fact]
    public void Corrected_previous_close_rebases_the_daily_seal_and_older_repairs_do_not_change_later_baselines()
    {
        var date=new DateOnly(2026,11,19);var boundary=FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime;
        var original=new StrategyPositionSnapshot{Id=StrategyPositionId.Create(new(101,701,1,1),TradeStrategyKind.IronCondor),
            StrategyKind=TradeStrategyKind.IronCondor,IsOpen=true,Phase=StrategyPositionPhase.EndOfDay,ValueDate=date,
            LatestSealedValueDate=date,PositionSequence=3,AsOfUtc=boundary,DailyOpeningPnl=0,
            Legs=[new(){TradeLegId=Guid.NewGuid(),ContractId="TEST",SignedQuantity=1,OpeningPrice=9,CurrentPrice=9,LastPriceAtUtc=boundary.AddHours(-1)}]};
        var previous=original with{ValueDate=date.AddDays(-1),UnrealizedPnl=1};
        var quote=new StoredPositionClosingQuote{EvidenceId=new string('A',64),SourceArchiveSha256=new string('B',64),
            ContractId="TEST",InstrumentId=1,SourceSequence=1,ValueDate=date,EventTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,
            ReceiveTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,Bid=10,Ask=12,BidSize=1,AskSize=1};
        var result=StoredQuotePositionEndOfDayRecovery.Recover(original,original,date,boundary,[quote],previous);
        Assert.Equal(1,result.Seal.DailyOpeningPnl);Assert.Equal(1,result.Seal.DailyPnlPoints);
        var later=original with{ValueDate=date.AddDays(1),LatestSealedValueDate=date.AddDays(1),DailyOpeningPnl=99,Phase=StrategyPositionPhase.MarkToMarket};
        var oldRepair=StoredQuotePositionEndOfDayRecovery.Recover(original,later,date,boundary,[quote],previous);
        Assert.Equal(99,oldRepair.Current.DailyOpeningPnl);Assert.Equal(later.Legs[0].LastPriceAtUtc,oldRepair.Current.Legs[0].LastPriceAtUtc);
    }

    [Theory]
    [InlineData(true, 10, 12, 11)]
    [InlineData(false, 0, 12, 12)]
    public void Closing_mark_uses_midpoint_or_explicit_offer_without_inventing_a_bid(bool hasBid, int bid, int ask, int expected)
    {
        var date = new DateOnly(2026, 11, 17);
        var boundary = FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime;
        var original = new StrategyPositionSnapshot {
            Id = StrategyPositionId.Create(new(101, 701, 1, 1), TradeStrategyKind.IronCondor),
            StrategyKind = TradeStrategyKind.IronCondor, IsOpen = true, Phase = StrategyPositionPhase.EndOfDay,
            ValueDate = date, LatestSealedValueDate = date, PositionSequence = 2, AsOfUtc = boundary,
            HistoryAsOfUtc = boundary, HistoryPositionSequence = 1,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "TEST", SignedQuantity = 1,
                OpeningPrice = 9, CurrentPrice = 9, LastPriceAtUtc = boundary.AddHours(-1) }]
        };
        var quote = new StoredPositionClosingQuote {
            EvidenceId = new string('A', 64), SourceArchiveSha256 = new string('B', 64), ContractId = "TEST",
            ValueDate = date, InstrumentId = 1, SourceSequence = 1, HasBid = hasBid, Bid = bid, Ask = ask,
            BidSize = hasBid ? 1u : 0u, AskSize = 1,
            EventTimestampNanoseconds = (boundary.AddMinutes(-1).Ticks - DateTime.UnixEpoch.Ticks) * 100,
            ReceiveTimestampNanoseconds = (boundary.AddMinutes(-1).Ticks - DateTime.UnixEpoch.Ticks) * 100
        };
        var result = StoredQuotePositionEndOfDayRecovery.Recover(original, original, date, boundary, [quote]);
        Assert.Equal(expected, result.Seal.Legs[0].CurrentPrice);
        Assert.Equal(quote.ObservedAtUtc, result.Seal.Legs[0].LastPriceAtUtc);
        var invalidFutureSeal = original with { Legs = [original.Legs[0] with { LastPriceAtUtc = boundary.AddSeconds(1) }] };
        var repaired = StoredQuotePositionEndOfDayRecovery.Recover(invalidFutureSeal, invalidFutureSeal, date, boundary, [quote]);
        Assert.Equal(quote.ObservedAtUtc, repaired.Seal.Legs[0].LastPriceAtUtc);
        var futureNs=(boundary.Ticks-DateTime.UnixEpoch.Ticks)*100+1;
        Assert.Throws<InvalidOperationException>(()=>StoredQuotePositionEndOfDayRecovery.Recover(original,original,date,boundary,
            [quote with {EventTimestampNanoseconds=futureNs,ReceiveTimestampNanoseconds=futureNs}]));
        Assert.Throws<InvalidOperationException>(() => StoredQuotePositionEndOfDayRecovery.Recover(original, original,
            date, boundary, [quote with { HasBid = false, Bid = 1, BidSize = 1 }]));
    }
}
