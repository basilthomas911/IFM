using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
/// <summary>Verifies dated rows, immutable finalization and daily versus cumulative PnL.</summary>
public sealed class StrategyPositionDailyLifecycleTests
{
    [Theory]
    [InlineData(TradeStrategyKind.IronCondor)]
    [InlineData(TradeStrategyKind.VerticalSpread)]
    [InlineData(TradeStrategyKind.FuturesOutright)]
    public void Daily_marks_replace_one_row_eod_seals_it_and_next_date_starts_another(TradeStrategyKind strategy)
    {
        var machine = new StrategyPositionActorStateMachine();
        var legId = Guid.NewGuid();
        var opened = new DateTime(2026, 10, 7, 14, 0, 0, DateTimeKind.Utc);
        var trade = new EstablishedTradeDefinition
        {
            Id = new(101,701,1701,1101), StrategyKind = strategy, Status = EstablishedTradeStatus.Open,
            Legs = [new() { TradeLegId = legId, ContractId = "TEST", SignedQuantity = 1 }],
            OriginalFills = [new() { TradeLegId = legId, ContractId = "TEST", SignedQuantity = 1, Price = 10 }]
        };
        Assert.True(machine.Open(trade, Guid.NewGuid(), opened).Accepted);
        var opening = machine.Current!;
        Assert.Equal(StrategyPositionPhase.Open, opening.Phase);
        Assert.True(machine.UpdateLeg(legId, 11, 1, opened.AddMinutes(1), 1).Accepted);
        var first = machine.Current!;
        Assert.True(machine.UpdateLeg(legId, 12, 2, opened.AddMinutes(2), 1).Accepted);
        Assert.Equal(first.HistoryAsOfUtc, machine.Current!.HistoryAsOfUtc);
        Assert.Equal(first.HistoryPositionSequence, machine.Current.HistoryPositionSequence);
        Assert.NotEqual(opening.HistoryPositionSequence, first.HistoryPositionSequence);
        var closeBoundary = new DateTime(2026, 10, 7, 21, 0, 0, DateTimeKind.Utc);
        var valueDate = new DateOnly(2026,10,7);
        var beforeEod = machine.Current;
        Assert.False(machine.EndOfDay(valueDate.AddDays(1), closeBoundary).Accepted);
        Assert.Same(beforeEod, machine.Current);
        Assert.True(machine.EndOfDay(valueDate, closeBoundary).Accepted);
        var sealedPosition = machine.Current!;
        Assert.Equal(valueDate, sealedPosition.ValueDate);
        Assert.Equal(valueDate, sealedPosition.LatestSealedValueDate);
        Assert.Equal(first.HistoryAsOfUtc, sealedPosition.HistoryAsOfUtc);
        Assert.Equal(opened.AddMinutes(2), sealedPosition.LastMarketObservationUtc);
        Assert.True(machine.EndOfDay(valueDate, closeBoundary).Accepted);
        Assert.Same(sealedPosition, machine.Current);
        Assert.False(machine.UpdateLeg(legId, 99, 3, opened.AddHours(2), 1).Accepted);
        Assert.Same(sealedPosition, machine.Current);
        Assert.True(machine.UpdateLeg(legId, 13, 3, new DateTime(2026,10,7,22,0,0,DateTimeKind.Utc), 1).Accepted);
        Assert.Equal(new DateOnly(2026,10,8), machine.Current!.ValueDate);
        Assert.Equal(3m, machine.Current.UnrealizedPnl);
        Assert.Equal(1m, machine.Current.DailyPnl);
        Assert.NotEqual(first.HistoryAsOfUtc, machine.Current.HistoryAsOfUtc);
        Assert.Equal(StrategyPositionPhase.EndOfDay, sealedPosition.Phase);
    }
    [Fact]
    public void No_tick_day_retains_actual_observation_time_and_seals_idempotently()
    {
        var opened = new DateTime(2026,10,7,14,0,0,DateTimeKind.Utc);
        var id = Guid.NewGuid();
        var machine = new StrategyPositionActorStateMachine();
        machine.Open(new() { Id = new(101,701,1701,1101), StrategyKind = TradeStrategyKind.FuturesOutright,
            Legs = [new() { TradeLegId = id, ContractId = "ES", SignedQuantity = 1 }],
            OriginalFills = [new() { TradeLegId = id, ContractId = "ES", SignedQuantity = 1, Price = 10 }] }, Guid.NewGuid(), opened);
        Assert.True(machine.EndOfDay(new DateOnly(2026,10,8), new DateTime(2026,10,8,21,0,0,DateTimeKind.Utc)).Accepted);
        Assert.Equal(opened, machine.Current!.LastMarketObservationUtc);
        Assert.Equal(new DateOnly(2026,10,8), machine.Current.ValueDate);
        Assert.Equal(0, machine.Current.DailyPnl);
    }
}
