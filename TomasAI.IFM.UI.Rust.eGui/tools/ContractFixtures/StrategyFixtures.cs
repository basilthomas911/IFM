using System.Collections;
using System.Reflection;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Stable workflow presentation oracle and generated display metadata. No live market data is requested.</summary>
internal static class StrategyFixtures {
 // A paired desktop-only clock can place identical observations inside both
 // clients' visible history windows. Strict protocol fixtures stay frozen.
 public static readonly DateTime At=DesktopFixtureMode.Enabled&&DateTime.TryParse(Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_STRATEGY_CLOCK_UTC"),null,System.Globalization.DateTimeStyles.RoundtripKind,out var desktopAt)
     ?desktopAt.ToUniversalTime():new(2026,10,26,12,30,0,DateTimeKind.Utc);
 public static readonly FuturesItiSignalV2ReadModel Signal=new(){ContractId="ESZ6",ValueDate=new(2026,10,26),TimeFrameStartValueDate=new(2026,10,26),TimePeriod=TimeFrameType.Daily,IntrinsicTime=At,IntrinsicPrice=6500.25,IntrinsicTimeMode=IntrinsicTimeModeType.TrendDirectionChanged,IntrinsicTimeTrend=IntrinsicTimeTrendType.UpTrend,SequenceId=5};
 public static readonly IntrinsicTimeStrategyWorkflowView View=new(){EntityId=IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId("ESZ6",new(2026,10,26),TimeFrameType.Daily)),WorkflowId=new(Guid.Parse("b7329333-f0e3-478e-b884-6d7ef9d47971")),WorkflowRevision=9,Status=WorkflowStrategyMachineStatus.Started,StartedAtUtc=At,UpdatedAtUtc=At.AddSeconds(1),ExpiresAtUtc=At.AddMinutes(2),WorkflowDefinitionVersion=1,TriggerEvent=new(){FuturesItiSignal=Signal,CreatedOn=At},RegimeDiscovery=new(){ProcessingStatus=StrategyActorProcessingStatus.Completed,ContinuationDecision=StrategyWorkflowContinuationDecision.Proceed,StartedAtUtc=At,CompletedAtUtc=At.AddMilliseconds(125),ContinuationReasonCodes=["fixture"]},MarketCondition=new(){ProcessingStatus=StrategyActorProcessingStatus.Processing}};
 static bool Scalar(Type type)=>type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(decimal)||type==typeof(Guid)||type==typeof(StrategyWorkflowId)||type==typeof(DateTime)||type==typeof(DateOnly)||type==typeof(TimeSpan);
 public static void Export(string output){
  FixtureFiles.WriteAllBytes(Path.Combine(output,"strategy-view.msgpack"),MessagePackSerializer.Serialize(View,MessagePackBinarySerializer.Options));
  var transported=MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(MessagePackSerializer.Serialize(View,MessagePackBinarySerializer.Options),MessagePackBinarySerializer.Options);
  using var adapter=new StrategyDetailsAdapter();var details=adapter.Project(StrategyWorkflowPresentation.CreateDetails(transported),1);
  FixtureFiles.WriteAllText(Path.Combine(output,"strategy-details-display.json"),JsonSerializer.Serialize(BridgeDocument.Project(details,camelCase:true)));
  FixtureFiles.WriteAllText(Path.Combine(output,"strategy-row-display.json"),JsonSerializer.Serialize(BridgeDocument.Project(StrategyDetailsAdapter.Display(StrategyWorkflowPresentation.CreateRow(transported),1000),camelCase:true)));
  var shapes=new Dictionary<string,object>();var seen=new HashSet<Type>();
  void Visit(Type type){type=Nullable.GetUnderlyingType(type)??type;if(!seen.Add(type)||Scalar(type))return;
   if(type.IsArray){shapes[type.ToString()]=new{name=type.Name,properties=Array.Empty<object>(),opaqueByteArray=Array.CreateInstance(type.GetElementType()!,0) is byte[]};Visit(type.GetElementType()!);return;}if(type.IsGenericType&&typeof(IEnumerable).IsAssignableFrom(type)){foreach(var argument in type.GetGenericArguments())Visit(argument);return;}if(type==typeof(ReadOnlyMemory<byte>))return;
   var union=type.GetCustomAttributes<UnionAttribute>().ToArray();if(union.Length>0){foreach(var entry in union)Visit(entry.SubType);return;}
   DisplayProperty[] properties;try{properties=DisplayDescriptors.For(type);}catch(NotSupportedException){return;}
   shapes[type.ToString()]=new{name=type.Name,properties=properties.Select(p=>new{name=p.Name,type=p.PropertyType.ToString(),p.DisplayName,p.IsBrowsable,key=type.GetProperty(p.Name)?.GetCustomAttribute<KeyAttribute>()?.IntKey}).ToArray()};foreach(var p in properties)Visit(p.PropertyType);
  }
  Visit(typeof(IntrinsicTimeStrategyWorkflowView));
  Visit(typeof(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskObservation));
  foreach(var type in new[]{typeof(TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel),typeof(TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel),typeof(TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundAllocationReadModel),typeof(TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundRiskEnvelopeReadModel),typeof(TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundTradeTemplateAssignmentReadModel),typeof(TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel)})Visit(type);
  Visit(typeof(TomasAI.IFM.UI.Net.ViewModels.Portfolio.FundFinancialViewState));
  Visit(typeof(TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialLedgerConfiguration));
  Visit(typeof(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskHistoryRow));
  FixtureFiles.WriteAllText(Path.Combine(output,"strategy-display-shapes.json"),JsonSerializer.Serialize(shapes));
 }
}
