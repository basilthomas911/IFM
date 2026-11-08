using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class HistoricalPositionEndOfDayTests
{
    [Fact]
    public void Historical_seal_keeps_last_observation_daily_baseline_and_closed_current_state()
    {
        var id=StrategyPositionId.Create(new(101,701,1901,1501),TradeStrategyKind.IronCondor);
        var date=new DateOnly(2026,10,26);
        var observed=new DateTime(2026,10,26,1,26,0,DateTimeKind.Utc);
        var mark=new StrategyPositionSnapshot { Id=id,StrategyKind=TradeStrategyKind.IronCondor,
            Phase=StrategyPositionPhase.MarkToMarket,IsOpen=true,PositionSequence=20,ValueDate=date,
            AsOfUtc=observed,LastMarketObservationUtc=observed,HistoryAsOfUtc=observed,HistoryPositionSequence=2,
            DailyOpeningPnl=1,UnrealizedPnl=3,ContractCashMultiplier=50,OpeningCommission=2.6m,OpeningValueDate=date.AddDays(-1),
            Legs=[new() { TradeLegId=Guid.NewGuid(),SignedQuantity=1,OpeningPrice=2,CurrentPrice=5,CashMultiplier=50 }] };
        var current=mark with { Phase=StrategyPositionPhase.Close,IsOpen=false,ValueDate=new(2026,11,6),PositionSequence=100 };
        var state=new IronCondorPositionCommandState();
        var command=new EndOfDayIronCondorPositionCommand { CommandId=Guid.NewGuid(),EntityId=id,
            Subject=new(ActorType.Command,"FuturesIronCondorTradePositionCommand",EndOfDayIronCondorPositionCommand.Verb,id.Format()),
            ValueDate=date,EffectiveAtUtc=FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime,
            HistoricalSnapshot=mark,HistoricalSourceEventId=123 };
        state.Update(new IronCondorPositionChangedEvent { EntityId=id,PositionSnapshot=current },command);
        state.AcceptChanges();
        command.Execute(state).Success.Should().BeTrue();
        state.Current.Should().Be(current);
        var fact=state.Events.OfType<IronCondorPositionChangedEvent>().Single();
        fact.HistoricalEndOfDay!.Phase.Should().Be(StrategyPositionPhase.EndOfDay);
        fact.HistoricalEndOfDay.LastMarketObservationUtc.Should().Be(observed);
        fact.HistoricalEndOfDay.HistoryAsOfUtc.Should().Be(observed);
        fact.HistoricalEndOfDay.DailyOpeningPnl.Should().Be(1);
        fact.HistoricalEndOfDay.DailyPnlPoints.Should().Be(2);
        fact.HistoricalEndOfDay.HistoryPositionSequence.Should().Be(2);
        fact.HistoricalSourceEventId.Should().Be(123);
        var invalid=()=>HistoricalPositionEndOfDay.Seal(mark with { ValueDate=date.AddDays(1) },current,date,command.EffectiveAtUtc);
        invalid.Should().Throw<InvalidOperationException>();
    }
}
