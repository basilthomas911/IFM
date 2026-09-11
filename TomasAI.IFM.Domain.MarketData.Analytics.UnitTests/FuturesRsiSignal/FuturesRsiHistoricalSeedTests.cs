using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;
public sealed class FuturesRsiHistoricalSeedTests
{
 static readonly CmeFuturesMarketSessionCalendar Calendar=new();
 static readonly DateTimeOffset Cutoff=new(2026,8,25,18,0,0,TimeSpan.Zero);
 [Theory]
 [InlineData(TimeFrameType.FifteenSeconds)][InlineData(TimeFrameType.OneMinute)][InlineData(TimeFrameType.FiveMinutes)]
 [InlineData(TimeFrameType.FifteenMinutes)][InlineData(TimeFrameType.OneHour)][InlineData(TimeFrameType.FourHours)][InlineData(TimeFrameType.Daily)]
 public void Complete_seed_matches_live_accumulation_and_replay_and_rejects_duplicate_handoff(TimeFrameType frame)
 {
  var command=Command(frame);var state=new FuturesRsiSignalCommandState();
  var result=command.Execute(state,Calendar);Assert.True(result.Success);
  FuturesRsiAccumulatorCheckpoint? expected=null;
  foreach(var bar in command.HistoricalSeed!.Observations)expected=FuturesRsiWilderAccumulator.Apply(expected,bar,14).Checkpoint;
  Assert.Equal(expected,state.AccumulatorCheckpoint);Assert.NotNull(expected!.CurrentRsi);Assert.Equal(100d,expected.CurrentRsi);
  Assert.True(Assert.Single(state.Events.OfType<FuturesRsiSignalGeneratedEvent>()).FuturesRsiSignal.IsWarm);
  var started=Assert.Single(state.Events.OfType<FuturesRsiSignalStartedEvent>());
  Assert.Equal(16,started.HistoricalSeedCount);
  Assert.Equal(16,MessagePackSerializer.Deserialize<FuturesRsiSignalStartedEvent>(MessagePackSerializer.Serialize(started)).HistoricalSeedCount);
  var restored=new FuturesRsiSignalCommandState();foreach(var fact in state.Events)restored.Apply(fact,false);
  Assert.Equal(state.AccumulatorCheckpoint,restored.AccumulatorCheckpoint);
  var duplicate=FuturesRsiWilderAccumulator.Apply(restored.AccumulatorCheckpoint,command.HistoricalSeed.Observations[^1],14);
  Assert.False(duplicate.IsApplied);
  var again=command with {CommandId=Guid.NewGuid()};Assert.True(again.Execute(restored,Calendar).Success);
  Assert.Equal(expected,restored.AccumulatorCheckpoint);
  var restoredEvent=Assert.Single(restored.Events.OfType<FuturesRsiSignalStartedEvent>());
  Assert.NotNull(restoredEvent.RestoredSignal);
  Assert.NotNull(MessagePackSerializer.Deserialize<FuturesRsiSignalStartedEvent>(MessagePackSerializer.Serialize(restoredEvent)).RestoredSignal);
  var nextWindow=RsiHistoricalSeedWindowModel.Create(frame,1,Cutoff.AddDays(frame==TimeFrameType.Daily?1:0).AddHours(frame==TimeFrameType.Daily?0:5),Calendar)[0];
  var last=command.HistoricalSeed.Observations[^1];
  var live=last with { ValueDate=nextWindow.ValueDate,IntervalStartUtc=nextWindow.StartUtc,IntervalEndUtc=nextWindow.EndUtc,
   ObservationId=FuturesTradeSessionBarId.Create(last.MarketSeriesIdentity,frame,nextWindow.EndUtc,1),Close=114m,LastSourceSequence=1 };
  var advanced=FuturesRsiWilderAccumulator.Apply(restored.AccumulatorCheckpoint,live,14);
  Assert.True(advanced.IsApplied);Assert.Equal(expected.ChangeCount+1,advanced.Checkpoint.ChangeCount);
  Assert.InRange(advanced.Checkpoint.CurrentRsi!.Value,0d,99.999d);
  Assert.False(FuturesRsiWilderAccumulator.Apply(advanced.Checkpoint,last,14).IsApplied);
  Assert.Equal("RSI.SEED_CHECKPOINT_PRESERVED",Assert.Single(restored.Events.OfType<FuturesRsiSignalStartedEvent>()).HistoricalSeedReason);
 }
 [Fact]public void Empty_or_gapped_history_applies_zero_accumulator_iterations()
 {
  var original=Command(TimeFrameType.OneHour);
  foreach(var observations in new[]{Array.Empty<FuturesTradeSessionBarReadModel>(),original.HistoricalSeed!.Observations.Skip(1).ToArray(),original.HistoricalSeed.Observations.Reverse().ToArray()})
  {
   var state=new FuturesRsiSignalCommandState();var command=original with {HistoricalSeed=original.HistoricalSeed with {Observations=observations}};
   Assert.True(command.Execute(state,Calendar).Success);Assert.Null(state.AccumulatorCheckpoint);
   Assert.Empty(state.Events.OfType<FuturesRsiSignalGeneratedEvent>());
  }
 }
 [Fact]public void Market_open_uses_previous_completed_sessions_and_daily_skips_weekends()
 {
  var monday=new DateOnly(2026,8,24);var open=Calendar.GetSession(monday).StartUtc;
  var intraday=RsiHistoricalSeedWindowModel.Create(TimeFrameType.FourHours,16,open,Calendar);
  Assert.Equal(16,intraday.Length);Assert.All(intraday,x=>Assert.True(x.EndUtc<=open));
  var daily=RsiHistoricalSeedWindowModel.Create(TimeFrameType.Daily,16,open,Calendar);
  Assert.All(daily,x=>Assert.True(Calendar.IsTradingDate(x.ValueDate)));Assert.True(daily[^1].ValueDate<monday);
 }
 static StartFuturesRsiSignalCommand Command(TimeFrameType frame)
 {
  var entity=FuturesRsiSignalEntityId.Create("ES-SEED",new DateOnly(2026,8,25),frame,14);var series=MarketSeriesIdentity.ForContract(entity.ContractId);
  var bars=RsiHistoricalSeedWindowModel.Create(frame,16,Cutoff,Calendar).Select((x,i)=>new FuturesTradeSessionBarReadModel{
   ContractId=entity.ContractId,MarketSeriesIdentity=series,ValueDate=x.ValueDate,TimeFrame=frame,IntervalStartUtc=x.StartUtc,IntervalEndUtc=x.EndUtc,
   ObservationId=FuturesTradeSessionBarId.Create(series,frame,x.EndUtc,0),Open=100+i,High=100+i,Low=100+i,Close=100+i,
   FirstMarketEventUtc=x.EndUtc.AddTicks(-1),LastMarketEventUtc=x.EndUtc.AddTicks(-1),CalculatedAtUtc=Cutoff,CalculationVersion="rsi-seed-test-v1",
   IsComplete=true,IsValid=true,CalculationMethod=MarketSignalCalculationMethod.NormalizedHistoricalAggregate}).ToArray();
  return MessagePackSerializer.Deserialize<StartFuturesRsiSignalCommand>(MessagePackSerializer.Serialize(new StartFuturesRsiSignalCommand(entity){CommandId=Guid.NewGuid(),HistoricalSeed=new(16,Cutoff,bars,"test")}));
 }
}
