using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
internal static class MonitorPublicationResponder{
 static long revision=10;
 public static bool Handles(string subject)=>subject=="Query.NativeCompatibility.MonitorPublish.none";
 public static async Task<byte[]> Handle(NatsConnection connection,byte[] payload,CancellationToken token){
  var options=MessagePackBinarySerializer.Options;var action=MessagePackSerializer.Deserialize<string>(payload,options);revision++;
  async Task Publish<T>(string route,T value)=>await connection.PublishAsync(route,MessagePackSerializer.Serialize(value,options),cancellationToken:token);
  if(DesktopFixtureMode.Enabled&&action is "PopulatedLoss" or "PopulatedGain"){
   var change=TradeRealtimeSnapshotChangedEvent.From(PopulatedMonitorFixture.Create(later:true,gain:action=="PopulatedGain"));await Publish(change.Subject.ToString(),change);
  }else if(action is "Newer" or "Stale" or "WrongScope"){
   var snapshot=MonitorFixtures.Realtime with{SnapshotRevision=action=="Stale"?1:revision,PublishedAtUtc=new(StrategyFixtures.At.AddSeconds(revision)),TradeId=action=="WrongScope"?new(1,3,701,456):MonitorFixtures.Id};
   var change=TradeRealtimeSnapshotChangedEvent.From(snapshot);await Publish(change.Subject.ToString(),change);
  }else if(action=="Eod"){
   var subject=new ActorSubject(ActorType.Event,"FuturesIronCondorTradePositionEvent","StrategyPositionChanged",MonitorFixtures.PositionId.Format());
   await Publish(subject.ToString(),new IronCondorPositionChangedEvent{Subject=subject,Id=Guid.NewGuid(),EntityId=MonitorFixtures.PositionId,EventId=revision,PositionSnapshot=MonitorFixtures.Mark with{Phase=StrategyPositionPhase.EndOfDay,PositionSequence=revision}});
  }else if(action=="Start"){
   var subject=new ActorSubject(ActorType.Event,"MarketDataFeedEvent","StartedComplete",MonitorFixtures.Date.ToString("yyyy-MM-dd"));
   await Publish(subject.ToString(),new MarketDataFeedStartedCompleteEvent{Subject=subject,Id=Guid.NewGuid(),EntityId=new(MonitorFixtures.Date),ValueDate=MonitorFixtures.Date,ReceivedOn=StrategyFixtures.At.AddSeconds(revision)});
  }else if(action=="Stop"){
   var subject=new ActorSubject(ActorType.Event,"MarketDataFeedEvent","StoppedComplete",MonitorFixtures.Date.ToString("yyyy-MM-dd"));
   await Publish(subject.ToString(),new MarketDataFeedStoppedCompleteEvent{Subject=subject,Id=Guid.NewGuid(),EntityId=new(MonitorFixtures.Date),ValueDate=MonitorFixtures.Date,ReceivedOn=StrategyFixtures.At.AddSeconds(revision)});
  }else if(action=="Fail"){
   var subject=new ActorSubject(ActorType.Event,"MarketDataFeedEvent","OptionFeedFailed",MonitorFixtures.Date.ToString("yyyy-MM-dd"));
   await Publish(subject.ToString(),new OptionMarketDataFeedFailedEvent{Subject=subject,Id=Guid.NewGuid(),EntityId=new(MonitorFixtures.Date),ErrorDate=StrategyFixtures.At.AddSeconds(revision),ErrorMessage="Synthetic terminal option failure",CleanupConfirmed=true});
  }else throw new InvalidDataException("Unsupported monitor publication");
  return MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,Value=new(true),ErrorMessage=""},options);
 }
}
