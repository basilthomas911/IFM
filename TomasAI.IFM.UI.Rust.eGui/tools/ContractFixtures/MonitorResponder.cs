using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
internal static class MonitorResponder{
 internal static TomasAI.IFM.Domain.MarketData.Shared.ViewModels.MarketSessionReadModel Session=null!;
 static string mode="Disabled";static bool enabled;static int hostCalls;
 static bool Populated=>DesktopFixtureMode.Enabled&&mode=="Populated";
 static TradeRealtimeSnapshot Current=>Populated?PopulatedMonitorFixture.Create():MonitorFixtures.Realtime;
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.Monitor",StringComparison.Ordinal)||mode=="ClosedSession"&&subject=="Query.MarketDataQuery.GetMarketSession.marketSession"||subject=="ifm.nativecompat.marketdata.v1.control"||mode!="Disabled"&&(subject.StartsWith("Query.FuturesOptionTradeQuery.GetIronCondorOptionTrade.",StringComparison.Ordinal)||subject.StartsWith("Query.FuturesOptionTradePositionQuery.GetIronCondorOptionTradePositionHistory.",StringComparison.Ordinal)||subject.StartsWith("Query.FuturesOptionTradeQuery.GetTradeRealtimeSnapshot.",StringComparison.Ordinal)||subject.StartsWith("Query.FuturesOptionTradeQuery.GetTradeLimit.",StringComparison.Ordinal)||subject.StartsWith("Query.IronCondorTradePlanQuery.",StringComparison.Ordinal)||subject.StartsWith("Query.FuturesOptionTickDataQuery.GetLastFuturesOptionTickData.",StringComparison.Ordinal));
 public static byte[]? Handle(string subject,byte[] payload){
  var options=MessagePackBinarySerializer.Options;byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,Value=value,ErrorMessage=""},options);
  if(subject=="Query.MarketDataQuery.GetMarketSession.marketSession")return Result(Session with{State=TomasAI.IFM.Domain.MarketData.Shared.FuturesMarketState.Closed,ActiveValueDate=null,AllowDevelopmentEmulator=false});
  if(subject=="Query.NativeCompatibility.MonitorMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,options);enabled=false;hostCalls=0;return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.MonitorHostCount.none")return Result(new ScalarReadModel<int>(hostCalls));
  if(subject=="ifm.nativecompat.marketdata.v1.control"){
   hostCalls++;var request=MessagePackSerializer.Deserialize<MarketDataHostRequest>(payload,options);var call=MessagePackSerializer.Deserialize<MarketDataApiCall>(request.Payload,MarketDataHostConnection.Serialization);
   if(request.OperationId==Guid.Empty||!DesktopFixtureMode.HostDeadline(request.DeadlineUtc)||call.Arguments.Length!=3||MessagePackSerializer.Deserialize<string>(call.Arguments[0],MarketDataHostConnection.Serialization)!=MonitorFixtures.Id.Format()||MessagePackSerializer.Deserialize<DateOnly>(call.Arguments[1],MarketDataHostConnection.Serialization)!=MonitorFixtures.Date||call.Arguments[2].Length!=0||call.Signature!=request.Path[4..]+"(System.String,System.DateOnly,System.Threading.CancellationToken)")throw new InvalidDataException("Native Host request changed scope, signature, API date or budget");
   if(mode=="HostTimeout")return null;
   bool result;if(request.Path=="api/EnableTradeFeedRecoveryAsync"){enabled=true;result=true;}else if(request.Path=="api/DisableTradeFeedRecoveryAsync"){enabled=false;result=true;}else if(request.Path=="api/LoadTradeFeedRecoveryAsync")result=enabled;else throw new InvalidDataException("Unexpected native Host operation");
   return MessagePackSerializer.Serialize(new MarketDataHostReply(MessagePackSerializer.Serialize(result,MarketDataHostConnection.Serialization),mode=="HostReject"?"Fixture recovery rejected":null),options);
  }
  if(subject.StartsWith("Query.FuturesOptionTickDataQuery.GetLastFuturesOptionTickData.",StringComparison.Ordinal))return Result<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel?>(null);
  if(subject.StartsWith("Query.FuturesOptionTradeQuery.GetIronCondorOptionTrade.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetIronCondorOptionTradeQuery>(payload,options);if(q.TradeId!=MonitorFixtures.Id||q.Subject.ToString()!=subject)throw new InvalidDataException("Wrong established trade request");return Result(mode=="WrongScope"?MonitorFixtures.Trade with{Id=new(1,3,701,456)}:Populated?PopulatedMonitorFixture.Trade:MonitorFixtures.Trade);
  }
  if(subject.StartsWith("Query.FuturesOptionTradePositionQuery.GetIronCondorOptionTradePositionHistory.",StringComparison.Ordinal)){
   if(mode=="HangPositions")return null;
   var q=MessagePackSerializer.Deserialize<GetIronCondorOptionTradePositionHistoryQuery>(payload,options);if(q.PositionId!=MonitorFixtures.PositionId||q.FromUtc!=DateTime.UnixEpoch||!DesktopFixtureMode.Clock(q.ToUtc,StrategyFixtures.At)||q.PageSize!=1000||!q.LoadAll)throw new InvalidDataException("Wrong API-bound history request");return Result(new[]{Populated?Current.Position!:MonitorFixtures.Mark});
  }
  if(subject.StartsWith("Query.FuturesOptionTradeQuery.GetTradeRealtimeSnapshot.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetTradeRealtimeSnapshotQuery>(payload,options);if(q.TradeId!=MonitorFixtures.Id||q.ValueDate!=MonitorFixtures.Date)throw new InvalidDataException("Wrong realtime identity/date");if(mode=="SnapshotReadFailed")return MessagePackSerializer.Serialize(new ServiceResult<TradeRealtimeSnapshot?>{Success=false,ErrorCode=59998,ErrorMessage="Synthetic initial snapshot read failure"},options);return Result(Current);
  }
  if(subject.StartsWith("Query.FuturesOptionTradeQuery.GetTradeLimit.",StringComparison.Ordinal))return Result(new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel{TradeId=456,TradeType=TradeType.ShortIronCondor,MaxLoss=1800m,MaxProfit=700m});
  if(subject.Contains(".GetCurrentIronCondorTradePlan.",StringComparison.Ordinal))return Result(Populated?Current.Plan!:MonitorFixtures.Plan);
  if(subject.Contains(".GetIronCondorTradePlanHistory.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetIronCondorTradePlanHistoryQuery>(payload,options);if(q.PlanId.Position!=MonitorFixtures.PositionId||q.PlanId.ValueDate!=MonitorFixtures.Date||(!DesktopFixtureMode.Enabled?q.Offset!=0:(q.Offset??0)!=0)||(!DesktopFixtureMode.Enabled?q.PageSize!=100:(q.PageSize<1||q.PageSize>200)))throw new InvalidDataException($"Wrong plan-page scope: id={q.PlanId}; expected={MonitorFixtures.PositionId}; date={q.PlanId.ValueDate}; offset={q.Offset}; size={q.PageSize}");return Result(new StrategyTradePlanHistoryPage([MonitorFixtures.Plan],null){Offset=0,MaximumRevision=mode=="WrongCut"?8:7,TotalCount=1});
  }
  if(subject.Contains(".GetIronCondorTradePlanChart.",StringComparison.Ordinal))return Result(new StrategyTradePlanChartSeries(StrategyFixtures.At.AddHours(-6),StrategyFixtures.At,[new(StrategyFixtures.At,-15m){ForwardSpread=-16m}]));
  throw new InvalidDataException("Unsupported monitor fixture query: "+subject);
 }
}
