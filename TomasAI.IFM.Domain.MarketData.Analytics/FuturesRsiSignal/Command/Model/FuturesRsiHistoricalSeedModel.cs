using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
public sealed record FuturesRsiSeedDecision(FuturesTradeSessionBarReadModel[] Observations,string Reason);
public static class FuturesRsiHistoricalSeedModel
{
 public static FuturesRsiSeedDecision Decide(StartFuturesRsiSignalCommand command,FuturesRsiAccumulatorCheckpoint? restored,IMarketSessionCalendar? calendar,DateTimeOffset now)
 {
  if(restored is not null)return new([],"RSI.SEED_CHECKPOINT_PRESERVED");
  if(command.HistoricalSeed is not {} seed)return new([],"RSI.SEED_NOT_REQUESTED");
  if(seed.RequestedPeriods<command.EntityId.PeriodLength+2||seed.RequestedPeriods>512||seed.Observations is null||seed.Observations.Length>512||
   seed.AsOfUtc==default||seed.AsOfUtc.Offset!=TimeSpan.Zero||seed.AsOfUtc>now||calendar is null)return new([],"RSI.SEED_INVALID_EMPTY");
  var windows=RsiHistoricalSeedWindowModel.Create(command.EntityId.TimePeriod,seed.RequestedPeriods,seed.AsOfUtc,calendar);
  if(seed.Observations.Length!=seed.RequestedPeriods||windows.Length!=seed.RequestedPeriods)return new([],"RSI.SEED_INCOMPLETE_EMPTY");
  for(var i=0;i<windows.Length;i++)
  {
   var bar=seed.Observations[i];var window=windows[i];
   if(bar is null||!bar.IsComplete||!bar.IsValid||bar.Close<=0||bar.ContractId!=command.EntityId.ContractId||bar.TimeFrame!=command.EntityId.TimePeriod||
    bar.LastMarketEventUtc<window.StartUtc||bar.LastMarketEventUtc>window.EndUtc||bar.ValueDate!=window.ValueDate||bar.IntervalStartUtc!=window.StartUtc||bar.IntervalEndUtc!=window.EndUtc||
    new FuturesTradeSessionBarReadModelValidationRules().Execute(bar).Length!=0)return new([],"RSI.SEED_NONCONTIGUOUS_EMPTY");
  }
  return new(seed.Observations,"RSI.SEED_APPLIED");
 }
}
