using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Synthetic display evidence, not historical market/pricing validation.</summary>
internal static class MonitorFixtures{
 internal static readonly TradeEntityId Id=new(1,2,701,456);
 internal static readonly DateOnly Date=new(2026,10,26);
 internal static readonly StrategyPositionId PositionId=StrategyPositionId.Create(Id,TradeStrategyKind.IronCondor);
 internal static EstablishedTradeDefinition Trade=>new(){Id=Id,StrategyKind=TradeStrategyKind.IronCondor,Status=EstablishedTradeStatus.Open,EvidenceRevision=1,EstablishedAtUtc=StrategyFixtures.At,Legs=ComposerOptionFixtures.Chain().Contracts.Select(c=>new TradeLegDefinition{TradeLegId=Guid.Parse(c.IsCall?c.ContractId.EndsWith("SHORT",StringComparison.Ordinal)?"11112233-4455-6677-8899-aabbccddeeff":"21112233-4455-6677-8899-aabbccddeeff":c.ContractId.EndsWith("SHORT",StringComparison.Ordinal)?"31112233-4455-6677-8899-aabbccddeeff":"41112233-4455-6677-8899-aabbccddeeff"),ContractId=c.ContractId,AssetFamily=TradeAssetFamily.FuturesOption,SignedQuantity=c.ContractId.EndsWith("SHORT",StringComparison.Ordinal)?-1:1,Strike=c.Strike,PutCall=c.IsCall?(byte)1:(byte)2,CashMultiplier=50,Expiry=Date.AddDays(4)}).ToArray(),OpeningHistoryValuation=new(Date,4,1,-14m,10m),OpeningPositionSnapshot=new(){Id=PositionId,StrategyKind=TradeStrategyKind.IronCondor,Phase=StrategyPositionPhase.Open,ValueDate=Date,AsOfUtc=StrategyFixtures.At,HistoryValuation=new(Date,4,1,-14m,10m)}};
 internal static StrategyPositionSnapshot Mark=>new(){Id=PositionId,StrategyKind=TradeStrategyKind.IronCondor,Phase=StrategyPositionPhase.MarkToMarket,ValueDate=Date,RouteGeneration=2,PositionSequence=10,AsOfUtc=StrategyFixtures.At.AddMinutes(1),HistoryAsOfUtc=StrategyFixtures.At.AddMinutes(1),HistoryValuation=new(Date,4,1,-15m,0m),DailyPnl=-50m};
 internal static TradeRealtimeSnapshot Realtime=>new(){TradeId=Id,PositionId=PositionId,ValueDate=Date,MonitoringGeneration=Guid.Parse("51112233-4455-6677-8899-aabbccddeeff"),SnapshotRevision=10,PublishedAtUtc=new(StrategyFixtures.At),CompositionVersion=1,Position=Mark,Legs=Trade.Legs.Select(l=>new TradeLegRealtimeSnapshot{Definition=l}).ToImmutableArray(),Status="Initializing"};
 internal static StrategyTradePlanSnapshot Plan=>new(){Position=Mark,ValueDate=Date,PlanRevision=7,CalculatedAtUtc=StrategyFixtures.At,IronCondorTradePlanSnapshot=new(){OrderId=701,TradeId=456,ValueDate=Date,SequenceId=7,ActionDateTime=StrategyFixtures.At,TradeType="ShortIronCondor",NetPrice=-15m,ForwardPrice=16m,Position=Mark}};
 internal static void Export(string output){
  var options=MessagePackBinarySerializer.ContentOptions;void Write<T>(string name,T value)=>FixtureFiles.WriteAllBytes(Path.Combine(output,name+".msgpack"),MessagePackSerializer.Serialize(value,options));
  T Round<T>(T value)=>MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value,options),options);
  FixtureFiles.WriteAllBytes(Path.Combine(output,"monitor-established-display.msgpack"),BridgeDocument.Encode(Round(Trade),camelCase:true,enumNames:true));
  FixtureFiles.WriteAllBytes(Path.Combine(output,"monitor-realtime-display.msgpack"),BridgeDocument.Encode(Round(Realtime),camelCase:true,enumNames:true));
  FixtureFiles.WriteAllBytes(Path.Combine(output,"monitor-plan-history-display.msgpack"),TradePlanDisplay.EncodeHistory(Round(Plan)));Write("monitor-plan",Plan);
  Write("monitor-limit-query",new TomasAI.IFM.Domain.Trade.Shared.Queries.GetTradeLimitQuery(456){Subject=new(TomasAI.IFM.Shared.EventModelActor.ActorType.Query,"FuturesOptionTradeQuery","GetTradeLimit","456")});
  var end=Mark with{Phase=StrategyPositionPhase.EndOfDay,PositionSequence=11};var late=Mark with{RouteGeneration=3,PositionSequence=99,DailyPnl=-999};var close=Mark with{Phase=StrategyPositionPhase.Close,PositionSequence=12};
  var inputs=new[]{Mark,end,late,close};Write("monitor-trade",Trade);Write("monitor-positions",inputs);Write("monitor-realtime",Realtime);
  var type=typeof(IronCondorViewModel);var vm=RuntimeHelpers.GetUninitializedObject(type);var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  void Field(string name,object value)=>type.GetField(name,flags)!.SetValue(vm,value);
  Field("<EstablishedTrade>k__BackingField",Trade);Field("_fundOrderTrade",new PortfolioFundOrderTradeEditorModel{TradeType=TradeType.ShortIronCondor});
  Field("_positionHistoryRows",new Dictionary<(DateOnly,StrategyPositionPhase),StrategyPositionSnapshot>());Field("_savedCurrencySnapshots",new Dictionary<DateOnly,IronCondorTradePlanSnapshot>());Field("_tradeHistory",new List<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeHistoryReadModel>());
  foreach(var input in inputs)type.GetMethod("RememberPositionHistory",flags)!.Invoke(vm,[input]);type.GetMethod("PublishPositionHistory",flags)!.Invoke(vm,null);
  FixtureFiles.WriteAllBytes(Path.Combine(output,"monitor-position-rows.msgpack"),BridgeDocument.Encode(((IronCondorViewModel)vm).TradeHistory,camelCase:true,enumNames:true));
 }
}
