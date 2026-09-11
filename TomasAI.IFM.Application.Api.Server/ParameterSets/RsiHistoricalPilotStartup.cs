using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Server.ParameterSets;
public sealed record RsiHistoricalPilotOptions(bool Enabled);
public interface IRsiHistoricalPilotStartup
{
 Task<ServiceResult<Guid>> StartAsync(FuturesRsiSignalEntityId entityId,CancellationToken token);
}
/// <summary>RSI-only retained-data pilot. Missing coverage starts empty; no acquisition/recovery worker is introduced.</summary>
public sealed class RsiHistoricalPilotStartup(IMarketDataAnalyticsCommandApi commands,IActorService actors,IDbContextFactory storage,
 IHistoricalObservationStore eod,IMarketSessionCalendar calendar,TimeProvider clock,ILogger<RsiHistoricalPilotStartup> logger,RsiHistoricalPilotOptions options):IRsiHistoricalPilotStartup
{
 public async Task<ServiceResult<Guid>> StartAsync(FuturesRsiSignalEntityId entityId,CancellationToken token)
 {
  if(!options.Enabled)return await commands.StartFuturesRsiSignalAsync(entityId).WaitAsync(token);
  // Attach before acquisition. A live observation/checkpoint arriving meanwhile takes precedence over seeding.
  var attached=await commands.StartFuturesRsiSignalAsync(entityId).WaitAsync(token);if(!attached.Success)return attached;
  var cutoff=clock.GetUtcNow();var count=entityId.PeriodLength+2;
  FuturesTradeSessionBarReadModel[] bars=[];var reason="RSI.SEED_HISTORY_UNAVAILABLE";
  try{bars=await ReadAsync(entityId,count,cutoff,token);reason=bars.Length==count?"RSI.SEED_CONTIGUOUS":"RSI.SEED_HISTORY_INCOMPLETE";}
  catch(Exception error)when(!token.IsCancellationRequested){logger.LogWarning(error,"RSI historical seed unavailable for {EntityId}; starting empty.",entityId);}
  var id=Guid.NewGuid();var command=new StartFuturesRsiSignalCommand(entityId){CommandId=id,PostEvents=true,
   Subject=new ActorSubject(ActorType.Command,StartFuturesRsiSignalCommand.Actor,StartFuturesRsiSignalCommand.Verb,entityId.Format()),
   HistoricalSeed=new(count,cutoff,bars,reason)};
  return await actors.RequestAsync<StartFuturesRsiSignalCommand,FuturesRsiSignalEntityId>(command,token);
 }
 public async Task<FuturesTradeSessionBarReadModel[]> ReadAsync(FuturesRsiSignalEntityId entity,int count,DateTimeOffset cutoff,CancellationToken token)
 {
  var windows=RsiHistoricalSeedWindowModel.Create(entity.TimePeriod,count,cutoff,calendar);var bars=new List<FuturesTradeSessionBarReadModel>();
  foreach(var window in windows)
  {
   token.ThrowIfCancellationRequested();decimal close;DateTimeOffset observed;
   if(entity.TimePeriod==TimeFrameType.Daily)
   {
    var source=await eod.GetRawEodAsync(MarketSeriesIdentity.ForContract(entity.ContractId),window.ValueDate,token);
    // Same unadjusted ES continuation used by the existing Daily analytics history.
    source??=await eod.GetRawEodAsync(MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId("ES","calendar-front","unadjusted",1)),window.ValueDate,token);
    if(source is not {IsComplete:true,IsValid:true}||source.SessionStartUtc!=window.StartUtc||source.SessionEndUtc!=window.EndUtc)return [];
    close=source.Close;observed=source.LastMarketEventUtc;
   }
   else
   {
    var tick=await storage.MarketDataDb.GetFuturesTickAtOrBeforeAsync(entity.ContractId,window.ValueDate,TimeOnly.FromDateTime(window.EndUtc.AddTicks(-1).UtcDateTime),token);
    if(tick is null||tick.Price<=0)return [];
    var session=calendar.GetSession(window.ValueDate);
    observed=new DateTimeOffset(DateOnly.FromDateTime(session.StartUtc.UtcDateTime).ToDateTime(tick.TickTime,DateTimeKind.Utc));
    if(observed<session.StartUtc)observed=observed.AddDays(1);
    if(observed<window.StartUtc||observed>=window.EndUtc)return [];
    close=tick.Price;
   }
   var series=MarketSeriesIdentity.ForContract(entity.ContractId);
   bars.Add(new(){MarketSeriesIdentity=series,ObservationId=FuturesTradeSessionBarId.Create(series,entity.TimePeriod,window.EndUtc,0),ContractId=entity.ContractId,
    ValueDate=window.ValueDate,TimeFrame=entity.TimePeriod,IntervalStartUtc=window.StartUtc,IntervalEndUtc=window.EndUtc,
    Open=close,High=close,Low=close,Close=close,FirstMarketEventUtc=observed,LastMarketEventUtc=observed,CalculatedAtUtc=cutoff,
    IsComplete=true,IsValid=true,CalculationVersion="rsi-historical-close-sample-v1",CalculationMethod=MarketSignalCalculationMethod.NormalizedHistoricalAggregate});
  }
  return bars.Count==count?bars.ToArray():[];
 }
}
