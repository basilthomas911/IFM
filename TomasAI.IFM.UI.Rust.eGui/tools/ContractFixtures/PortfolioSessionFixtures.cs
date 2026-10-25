using System.Reflection;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
/// <summary>Read-only managed-session oracle. Unknown operations throw; no producer, storage or live service is created.</summary>
public class PortfolioFixtureProxy:DispatchProxy {
 internal static bool PopulatedPolicy;
 internal static bool EditorChoices;
 internal static bool FundChoices;
 protected override object? Invoke(MethodInfo? method,object?[]? args){
  if(method!.Name=="get_MarketDataQueries")return new TomasAI.IFM.UI.Net.Services.MarketData.MarketDataQueryService(
   DispatchProxy.Create<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi,PortfolioFixtureProxy>(),
   DispatchProxy.Create<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi,PortfolioFixtureProxy>());
  if(method!.Name.StartsWith("get_",StringComparison.Ordinal)&&method.ReturnType.IsInterface)return Create(method.ReturnType,typeof(PortfolioFixtureProxy));
  var resultType=method.ReturnType.GetGenericArguments().Single();
  if(resultType==typeof(DateOnly))return Task.FromResult(new DateOnly(2026,10,26));
  if(method.Name=="ListAsync"&&resultType.IsGenericType&&resultType.GetGenericTypeDefinition()==typeof(IReadOnlyList<>))return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null,[Array.CreateInstance(resultType.GetGenericArguments()[0],0)]);
  var valueType=resultType.GetProperty("Value")!.PropertyType;
  object? value=method.Name switch{
   "GetValueDateAsync"=>new TomasAI.IFM.Shared.EventSourcing.ScalarReadModel<DateOnly>(new(2026,10,26)),
   "GetPortfoliosAsync"=>new PortfolioPage<PortfolioReadModel>{Items=[PortfolioFixtures.Portfolio],PageSize=100},
   "GetFundsAsync"=>new PortfolioPage<FundMandateReadModel>{Items=[FundChoices?PortfolioResponder.EditableFund:PortfolioFixtures.Fund],PageSize=100},
   "GetPortfolioRevisionAsync"=>new PortfolioAggregateRevision{PortfolioId=1,Revision=3,SourceEventId=30},
   "GetFundRevisionAsync"=>new PortfolioAggregateRevision{PortfolioId=1,FundId=2,Revision=4,SourceEventId=40},
   "GetFundAllocationAsync" or "GetFundRiskEnvelopeAsync"=>null,
   "GetAssignmentsAsync"=>Array.Empty<FundTradeTemplateAssignmentReadModel>(),
   "GetPoliciesAsync"=>new PortfolioPage<PortfolioFinancialPolicyReadModel>{Items=PopulatedPolicy?[PortfolioFixtures.Policy]:[],PageSize=200},
   "GetHistoryAsync"=>new RiskHistoryPage([new(1,2,(DateOnly)args![2]!,StrategyFixtures.At,StrategyFixtures.View.WorkflowId.Value,InspectionFixtures.Invocation,9,"Rejected",2,"Fixture risk history","Daily",123,"IronCondor")],"fixture-next"),
   "GetInvocationAsync"=>MessagePack.MessagePackSerializer.Deserialize<RiskObservation>(MessagePack.MessagePackSerializer.Serialize(InspectionFixtures.Observation,TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options),TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options),
   "GetFundPerformanceMetricsAsync"=>new FinancialRead<FundPerformanceMetrics>(FinancialReadStatus.Found,FinancialFixtures.Report,7,StrategyFixtures.At),
   "GetAccountBalancesAsync"=>FinancialLedgerFixtures.Read(FinancialLedgerFixtures.Balance),
   "GetFundTransactionsPageAsync"=>FinancialLedgerFixtures.Read(FinancialLedgerFixtures.Transactions(((GetFundTransactionsPageRequest)args![1]!).Cursor)),
   "GetFundReservationsPageAsync"=>FinancialLedgerFixtures.Read(FinancialLedgerFixtures.Reservations(((GetFundReservationsPageRequest)args![1]!).Cursor)),
   "GetJournalAsync"=>FinancialLedgerFixtures.Read(FinancialLedgerFixtures.Journal),
   "GetFinancialPostingConfigurationAsync"=>FinancialLedgerFixtures.Read(FinancialLedgerFixtures.Posting(((GetFinancialPostingConfigurationRequest)args![1]!).AccountingDate)),
   "GetPostingReceiptAsync"=>new FinancialRead<FinancialOperationOutcome>(FinancialReadStatus.NotFound,null,7,StrategyFixtures.At),
   "GetFinancialLedgerConfigurationAsync"=>FinancialLedgerFixtures.Read(FinancialConfigurationFixtures.Ledger),
   "PrepareFinancialBookAsync"=>FinancialLedgerFixtures.Read(FinancialConfigurationFixtures.Setup((PrepareFinancialBookRequest)args![1]!)),
   "PrepareFinancialAuthorityAsync"=>FinancialLedgerFixtures.Read(FinancialConfigurationFixtures.Authority(((PrepareFinancialAuthorityRequest)args![1]!).PermitNewSpending)),
   "GetStrategyDeploymentChoicesAsync"=>EditorChoices||FundChoices?new[]{PortfolioResponder.Deployment}:Array.CreateInstance(valueType.GetElementType()!,0),
   "GetLookupDefinitionsAsync"=>PortfolioResponder.LookupChoices((string)args![0]!),
   "GetTradeStrategySymbolsAsync"=>new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel[]{new(){Id=1,Symbol="ES",Currency="USD",Exchange="CME",Description="E-mini S&P 500"}},
   "AllocatePolicyIdAsync"=>new PortfolioBusinessIdAllocation{Kind=PortfolioBusinessIdentityKind.Policy,Value=100,CorrelationId=PortfolioFixtures.Key},
   "AllocateFundIdAsync"=>new PortfolioBusinessIdAllocation{Kind=PortfolioBusinessIdentityKind.Fund,Value=300,CorrelationId=PortfolioFixtures.Key},
   _=>throw new NotSupportedException("Portfolio session fixture does not execute "+method.Name)
  };
  var service=Activator.CreateInstance(resultType)!;resultType.GetProperty("Success")!.SetValue(service,true);resultType.GetProperty("ErrorMessage")!.SetValue(service,"");resultType.GetProperty("Value")!.SetValue(service,value);
  var task=typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null,[service]);
  return method.ReturnType.GetGenericTypeDefinition()==typeof(ValueTask<>)?Activator.CreateInstance(method.ReturnType,task):task;
 }
}
internal static class PortfolioSessionFixtures {
 public static async Task Export(string output){
  foreach(var populated in new[]{false,true}){
  PortfolioFixtureProxy.PopulatedPolicy=populated;
  var services=DispatchProxy.Create<IUiServiceCatalog,PortfolioFixtureProxy>();
  var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net.PortfolioSession")!;
  using var session=(IDisposable)type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==3).Invoke([services,null,null]);
  var request=new ContractEditorIntent(1,PortfolioFixtures.Key,"portfolio","load",0,null,[]);
  var result=await (Task<ContractEditorResult>)type.GetMethod("ExecuteAsync")!.Invoke(session,[request])!;
  if(result.Context?.GetValueOrDefault("from")!="2026-01-01"||result.Context?.GetValueOrDefault("portfolioName")!=PortfolioFixtures.Portfolio.Name||result.Context?.GetValueOrDefault("status")!="")throw new InvalidDataException("Managed Portfolio oracle did not load successfully: "+result.Context?.GetValueOrDefault("status"));
  FixtureFiles.WriteAllBytes(Path.Combine(output,populated?"portfolio-session-policy.msgpack":"portfolio-session-initial.msgpack"),result.Documents["portfolio"]);
  }
  PortfolioFixtureProxy.PopulatedPolicy=false;
  PortfolioFixtureProxy.EditorChoices=true;
  try {
   var services=DispatchProxy.Create<IUiServiceCatalog,PortfolioFixtureProxy>();
   var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net.PortfolioSession")!;
   using var session=(IDisposable)type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==3).Invoke([services,null,null]);
   async Task<ContractEditorResult> Execute(string action,Dictionary<string,string> fields)=>(await (Task<ContractEditorResult>)type.GetMethod("ExecuteAsync")!.Invoke(session,[new ContractEditorIntent(1,Guid.NewGuid(),"portfolio",action,0,null,fields)])!);
   await Execute("load",[]);
   var draft=await Execute("portfolio-policy",new(){["action"]="New Policy"});
   if(draft.Context?.GetValueOrDefault("status")!="")throw new InvalidDataException("Managed policy draft failed: "+draft.Context?.GetValueOrDefault("status"));
   FixtureFiles.WriteAllBytes(Path.Combine(output,"portfolio-session-policy-new.msgpack"),draft.Documents["portfolio"]);
  } finally {PortfolioFixtureProxy.EditorChoices=false;}
  PortfolioFixtureProxy.FundChoices=true;
  try{
   foreach(var view in new[]{"new-fund","version-fund","allocation","envelope","assignment"}){
    var services=DispatchProxy.Create<IUiServiceCatalog,PortfolioFixtureProxy>();
    var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net.PortfolioSession")!;
    using var session=(IDisposable)type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==3).Invoke([services,null,null]);
    async Task<ContractEditorResult> Execute(string action,Dictionary<string,string> fields)=>(await (Task<ContractEditorResult>)type.GetMethod("ExecuteAsync")!.Invoke(session,[new ContractEditorIntent(1,Guid.NewGuid(),"portfolio",action,0,null,fields)])!);
    await Execute("load",[]);var draft=await Execute("portfolio-open",new(){["view"]=view});
    if(draft.Context?.GetValueOrDefault("status")!="")throw new InvalidDataException("Managed Fund editor failed: "+view+": "+draft.Context?.GetValueOrDefault("status"));
    FixtureFiles.WriteAllBytes(Path.Combine(output,$"portfolio-session-{view}.msgpack"),draft.Documents["portfolio"]);
   }
  } finally {PortfolioFixtureProxy.FundChoices=false;}
  {
   var services=DispatchProxy.Create<IUiServiceCatalog,PortfolioFixtureProxy>();
   var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net.PortfolioSession")!;
   // Explicit empty stores prevent this development oracle from reading the
   // operator's real pending operations when opening a financial panel.
   using var session=(IDisposable)type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==3).Invoke([services,DispatchProxy.Create<TomasAI.IFM.UI.Net.ViewModels.Portfolio.IPendingFinancialOperationStore,PortfolioFixtureProxy>(),DispatchProxy.Create<TomasAI.IFM.UI.Net.ViewModels.Portfolio.IPendingFinancialConfigurationStore,PortfolioFixtureProxy>()]);
   async Task<ContractEditorResult> Execute(string action,Dictionary<string,string> fields)=>(await (Task<ContractEditorResult>)type.GetMethod("ExecuteAsync")!.Invoke(session,[new ContractEditorIntent(1,Guid.NewGuid(),"portfolio",action,0,null,fields)])!);
   await Execute("load",[]);var report=await Execute("portfolio-open",new(){["view"]="financials"});
   FixtureFiles.WriteAllBytes(Path.Combine(output,"portfolio-session-financials.msgpack"),report.Documents["portfolio"]);
   await Execute("portfolio-financial",new(){["action"]="Transaction"});
   var posting=await Execute("portfolio-choice",new(){["accountingDate"]="2026-10-26",["valueDate"]="2026-10-26"});
   FixtureFiles.WriteAllBytes(Path.Combine(output,"portfolio-session-posting.msgpack"),posting.Documents["portfolio"]);
  }
  foreach(var view in new[]{"ledger","account-version","rule-version","setup","setup-prepared","authority","authority-prepared","risk-policy","discard-policy","fund-financials","fund-risk-history","fund-risk-evidence"}){
   var services=DispatchProxy.Create<IUiServiceCatalog,PortfolioFixtureProxy>();var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net.PortfolioSession")!;
   using var session=(IDisposable)type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==3).Invoke([services,DispatchProxy.Create<TomasAI.IFM.UI.Net.ViewModels.Portfolio.IPendingFinancialOperationStore,PortfolioFixtureProxy>(),DispatchProxy.Create<TomasAI.IFM.UI.Net.ViewModels.Portfolio.IPendingFinancialConfigurationStore,PortfolioFixtureProxy>()]);
   async Task<ContractEditorResult> Execute(string action,Dictionary<string,string> fields,string? original=null)=>(await (Task<ContractEditorResult>)type.GetMethod("ExecuteAsync")!.Invoke(session,[new ContractEditorIntent(1,Guid.NewGuid(),"portfolio",action,0,original,fields)])!);
   await Execute("load",[]);ContractEditorResult result;
   if(view.StartsWith("fund-",StringComparison.Ordinal)){
    result=await Execute("portfolio-fund-view",new(){["view"]=view=="fund-financials"?"financials":"risk-history"});
    if(view!="fund-financials"){
     await Execute("portfolio-choice",new(){["date"]="2026-10-26"});
     result=await Execute("portfolio-financial",new(){["action"]="Refresh",["date"]="2026-10-26"});
     if(view=="fund-risk-evidence")result=await Execute("portfolio-grid",new(){["grid"]="risk-history"},InspectionFixtures.Invocation.ToString());
    }
   }else if(view is "risk-policy" or "discard-policy"){
    result=await Execute("portfolio-open",new(){["view"]="risk-policy"});
    if(view=="discard-policy"){
     await Execute("portfolio-policy",new(){["action"]="New Policy"});
     result=await Execute("portfolio-cancel",new(){["dirty"]="true"});
    }
   }else if(view is "ledger" or "account-version" or "rule-version"){
    await Execute("portfolio-open",new(){["view"]="ledger"});result=await Execute("portfolio-choice",new(){["start"]="2026-10-26",["end"]="2026-10-26"});
    if(view=="account-version")result=await Execute("portfolio-financial",new(){["action"]="Apply",["control"]="AddAccountVersion",["account"]="101"});
    if(view=="rule-version"){
     await Execute("portfolio-financial",new(){["action"]="Apply",["control"]="AddPostingRuleVersion",["rule"]=FinancialLedgerFixtures.DepositRule.RuleId.ToString()});
     result=await Execute("portfolio-choice",new(){["effective"]="2026-10-26"});
    }
   }else if(view.StartsWith("setup",StringComparison.Ordinal)){
    result=await Execute("portfolio-open",new(){["view"]="setup"});
    if(view=="setup-prepared")result=await Execute("portfolio-financial",new(){["action"]="Prepare",["account"]="paper:fixture",["start"]="2026-01-01",["end"]="2026-12-31"});
   }else{
    result=await Execute("portfolio-open",new(){["view"]="authority"});
    if(view=="authority-prepared")result=await Execute("portfolio-financial",new(){["action"]="Prepare",["permit"]="true"});
   }
   FixtureFiles.WriteAllBytes(Path.Combine(output,$"portfolio-session-{view}.msgpack"),result.Documents["portfolio"]);
  }
 }
}
