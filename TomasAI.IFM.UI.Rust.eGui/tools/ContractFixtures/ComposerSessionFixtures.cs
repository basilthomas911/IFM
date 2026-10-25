using System.Reflection;
using System.Runtime.CompilerServices;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Runs the unchanged hybrid SnapshotAsync over frozen source models
/// and read-only API proxies. No scope constructor, producer, subscription,
/// storage, execution command or live account is created.</summary>
public class ComposerFixtureProxy:DispatchProxy {
 internal static bool Options;
 internal static readonly DesktopTradeExecutionContext Execution=new("IFM-EMULATOR-PAPER",BrokerEnvironment.Emulator);
 internal static BrokerAccountDefinition Account=>MessagePackSerializer.Deserialize<BrokerAccountDefinition>(MessagePackSerializer.Serialize(new BrokerAccountDefinition{Id=new("IFM-EMULATOR-PAPER"),Gate=BrokerAccountOperationalGate.Open,Revision=3,DevelopmentQualificationsExempt=true},MessagePackBinarySerializer.Options),MessagePackBinarySerializer.Options);
 protected override object? Invoke(MethodInfo? method,object?[]? args){
  if(method!.Name=="get_TradeExecution")return Execution;
  if(method.Name=="get_AppEnvironment")return "Development";
  if(method.Name=="get_Services")return Create(typeof(IUiServiceCatalog),typeof(ComposerFixtureProxy));
  if(method.Name=="get_BrokerAccounts")return Create(typeof(IBrokerAccountQueryApi),typeof(ComposerFixtureProxy));
  if(method.Name=="GetAsync"&&method.ReturnType==typeof(ValueTask<ServiceResult<BrokerAccountDefinition>>))return ValueTask.FromResult(new ServiceResult<BrokerAccountDefinition>{Success=true,Value=Account,ErrorMessage=""});
  if(method.Name=="GetTradeOrderPreviewAsync"){
   var input=(TradeOrderPreviewRequest)args![0]!;
   return Task.FromResult(new ServiceResult<TradeOrderPreview>{Success=true,ErrorMessage="",Value=Options?ComposerOptionFixtures.Preview(input.InputRevision,input.EnteredOrderPrice):new(){InputRevision=input.InputRevision,OrderPrice=input.EnteredOrderPrice??6050.25m,NetMid=6050.25m,NetDelta=50,NetVega=0,ResolvedTickIncrement=.25m}});
  }
  throw new NotSupportedException("Unexpected source composer dependency "+method.Name);
 }
}
internal static class ComposerSessionFixtures {
 public static async Task Export(string output,FuturesContractV3ReadModel underlying){
  await ExportOne(output,underlying,false);await ExportOne(output,underlying,true);ComposerFixtureProxy.Options=false;
 }
 static async Task ExportOne(string output,FuturesContractV3ReadModel underlying,bool option){
  ComposerFixtureProxy.Options=option;
  var date=new DateOnly(2026,10,26);var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  void Field(object target,string name,object? value)=>target.GetType().GetField(name,flags)!.SetValue(target,value);
  var root=DispatchProxy.Create<IAppRoot,ComposerFixtureProxy>();
  await using var editor=new TradeOrderEditorViewModel(DispatchProxy.Create<IAppRoot,PortfolioFixtureProxy>(),date,[underlying],DispatchProxy.Create<IReferenceDataService,PortfolioFixtureProxy>(),brokerEnvironment:BrokerEnvironment.Emulator);
  var trade=new PortfolioFundOrderTradeEditorModel{PortfolioId=1,FundId=2,OrderId=123,TradeId=456,TradeType=option?TradeType.ShortIronCondor:TradeType.FuturesOutright,TradeAction=option?TradeAction.Sell:TradeAction.Buy,TradeState=TradeState.NewTrade,RequestedTradeDate=date,RequestedMaturityDate=option?date.AddDays(4):null,BaseContractId="ESZ6",BaseContractSymbol="ES"};
  var order=new PortfolioFundOrderEditorModel(TradeWorkspaceResponder.Order);order.Add(trade);
  Field(editor,"_portfolios",new[]{PortfolioFixtures.Portfolio});Field(editor,"_funds",new[]{new PortfolioFundEditorModel(2,PortfolioFixtures.Fund.Name,PortfolioFixtures.Fund.Objective,0,true,PortfolioFixtures.Fund.CreatedOnUtc,PortfolioFixtures.Fund.CreatedBy)});
  Field(editor,"_fundOrders",new[]{order});Field(editor,"_fundOrderTrades",new[]{trade});
  foreach(var name in new[]{"_portfolioSelectedIndex","_fundSelectedIndex","_fundOrderSelectedIndex","_fundOrderTradeSelectedIndex"})Field(editor,name,0);
  editor.ApplyMarketSessionSnapshot(MessagePackSerializer.Deserialize<ServiceResult<MarketSessionReadModel>>(File.ReadAllBytes(Path.Combine(output,"market-session-result.wire.msgpack")),MessagePackBinarySerializer.Options).Value!);
  var model=new BrokerManualTradeOrderViewModel(root,1,order,trade,underlying);
  var scope=RuntimeHelpers.GetUninitializedObject(typeof(RustClientServiceScope));
  Field(scope,"<TradeExecution>k__BackingField",ComposerFixtureProxy.Execution);Field(scope,"<DiagnosticLogger>k__BackingField",NullLogger.Instance);
  var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net.TradeComposerSession")!;
  var formatter=RuntimeHelpers.GetUninitializedObject(type);
  foreach(var pair in new Dictionary<string,object?>{
   ["scope"]=scope,["model"]=model,["editor"]=editor,["trade"]=trade,["underlying"]=underlying,["defaultsLoaded"]=true,["quantity"]=1,["sessionId"]="f0000000000000000000000000000001",["expiry"]=date,["apiValueDate"]=date,["status"]="",["reportPath"]="",["reportHash"]="",["reportPreview"]="",["lastExecutionEvidence"]="",["executionAction"]="Open",["putDelta"]=16m,["callDelta"]=16m,["putWidth"]=50m,["callWidth"]=50m,["brokerOrders"]=Array.Empty<TomasAI.IFM.Domain.Trade.Shared.Order.Broker.BrokerOrderDefinition>(),["executions"]=Array.Empty<OrderExecutionDefinition>(),["legs"]=new[]{new BrokerTradeLegData("ESZ6","+Future",null,null,null,null,false,Multiplier:decimal.Parse(underlying.Multiplier,System.Globalization.CultureInfo.InvariantCulture),IsFuture:true)},
   ["previewQuery"]=new BrokerTradePreviewQuery(new TradeQueryService(DispatchProxy.Create<ITradeQueryApi,ComposerFixtureProxy>())),
   ["initialization"]=new BrokerTradeInitializationResult(1,2,123,456,new(FinancialReadStatus.Found,new(8,"Open",[],0,123456.78m,true),10,StrategyFixtures.At),new(FinancialReadStatus.Found,new([]){ReservedFundLossCapital=321.25m},10,StrategyFixtures.At),new(){PortfolioId=1,FundId=2,AvailableCapital=10000,MaximumRiskPerTrade=1000},TradeWorkspaceResponder.Order,null,null,underlying,new(){ContractId="ESZ6",ValueDate=date,ClosePrice=6050.25m,DailyStdDevAmount=25},[],TomasAI.IFM.Application.TradeBroker.Contracts.BrokerCapabilities.Emulator("IFM-EMULATOR-PAPER"),ComposerFixtureProxy.Account,[])
  })Field(formatter,pair.Key,pair.Value);
  Field(formatter,"pendingMutations",Activator.CreateInstance(type.GetField("pendingMutations",flags)!.FieldType));
  Field(formatter,"expirations",Array.Empty<OptionContractExpiryReadModel>());
  if(option){
   var chain=MessagePackSerializer.Deserialize<EvaluatedOptionChainReadModel>(MessagePackSerializer.Serialize(ComposerOptionFixtures.Chain(),MessagePackBinarySerializer.Options),MessagePackBinarySerializer.Options);
   Field(formatter,"chain",chain);Field(formatter,"displayChain",chain);Field(formatter,"expiry",chain.ExpiryDate);
   Field(formatter,"legs",chain.Contracts.Select(c=>new BrokerTradeLegData(c.ContractId,c.ContractId.EndsWith("SHORT",StringComparison.Ordinal)?c.IsCall?"-SC":"-SP":c.IsCall?"+LC":"+LP",c.Strike,c.Bid,c.Ask,c.Delta,c.IsCall,c.Vega,50,c.QuoteAtUtc,true)).ToArray());
   Field(formatter,"expirations",new[]{new OptionContractExpiryReadModel{Symbol="ES",ContractId="ESZ6",ExpiryDate=chain.ExpiryDate,ProviderRoot="EW",RefreshedAtUtc=StrategyFixtures.At}});
  }
  var task=(Task)type.GetMethod("SnapshotAsync")!.Invoke(formatter,null)!;await task;
  var snapshot=((ContractEditorRow[] Rows,Dictionary<string,string[]> Choices,Dictionary<string,string> Context))task.GetType().GetProperty("Result")!.GetValue(task)!;
  FixtureFiles.WriteAllBytes(Path.Combine(output,option?"composer-options-session.msgpack":"composer-futures-session.msgpack"),MessagePackSerializer.Serialize(new ContractEditorResult("trade-orders",snapshot.Rows,snapshot.Choices,Context:snapshot.Context),MessagePackBinarySerializer.ContentOptions));
  if(!option)BrokerOrderFixtures.Export(output,formatter);
 }
}
