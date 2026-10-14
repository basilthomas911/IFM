using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Services.Analytics;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Read-only live strategy session using the same C# transport/service/view model as WinForms.</summary>
sealed class LiveStrategySession : IDisposable
{
 readonly RustClientServiceScope services;
 readonly CancellationTokenSource stopping=new();
 StrategyOperationsViewModel viewModel;
 int selectedTimeFrame=1;
 Task startup;
 readonly Dictionary<uint,TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId> selections=[];
 readonly Dictionary<TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId,uint> tokens=[];
 uint nextSelection=1000;
 public long ChangeVersion;
 public LiveStrategySession()
 {
  var uri=Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222";
  services=new RustClientServiceScope(uri);
  var service=services.Services.StrategyOperations;
  var date=DateOnly.TryParse(Environment.GetEnvironmentVariable("IFM_RUST_VALUE_DATE"),out var valueDate)?valueDate:DateOnly.FromDateTime(DateTime.UtcNow);
  viewModel=StrategyOperationsViewModelComposition.Create(service,Environment.GetEnvironmentVariable("IFM_RUST_SYMBOL")??"ES",Environment.GetEnvironmentVariable("IFM_RUST_CONTRACT")??"ES",date);
  viewModel.PropertyChanged+=(_,_)=>Interlocked.Increment(ref ChangeVersion);
  startup=Task.Run(StartAsync);
 }
 async Task StartAsync()
 {
  await services.StartAsync(stopping.Token);
  var query=services.Container.GetInstance<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi>();
  var symbol=Environment.GetEnvironmentVariable("IFM_RUST_SYMBOL")??"ES";
  var contract=Environment.GetEnvironmentVariable("IFM_RUST_CONTRACT");
  if(string.IsNullOrWhiteSpace(contract)){
   var active=await query.GetOnTheRunFuturesContractAsync(symbol).WaitAsync(stopping.Token);
   if(!active.Success||active.Value is null)throw new InvalidOperationException("Current futures contract unavailable: "+active.ErrorMessage);
   contract=active.Value.ContractId;
  }
  DateOnly date;
  if(!DateOnly.TryParse(Environment.GetEnvironmentVariable("IFM_RUST_VALUE_DATE"),out date)){
   var operational=await query.GetValueDateAsync().WaitAsync(stopping.Token);
   if(!operational.Success||operational.Value is null)throw new InvalidOperationException("Operational value date unavailable: "+operational.ErrorMessage);
   date=operational.Value.Value;
  }
  await viewModel.DisposeAsync();
  viewModel=StrategyOperationsViewModelComposition.Create(services.Services.StrategyOperations,symbol,contract,date);
  viewModel.PropertyChanged+=(_,_)=>Interlocked.Increment(ref ChangeVersion);
  viewModel.SelectedTimeFrame=(TimeFrameType)Volatile.Read(ref selectedTimeFrame);
  await viewModel.InitializeAsync(stopping.Token);Interlocked.Increment(ref ChangeVersion);
 }
 public string Status=>startup.IsFaulted?"Disconnected: "+startup.Exception!.GetBaseException().Message:!startup.IsCompleted?"Connecting and loading workflows":viewModel.LastError is { } error?error.Caption+": "+error.Message:viewModel.StatusText;
 public void Select(uint action)
 {
  if(action==2&&startup.IsCompleted){var failed=startup.IsFaulted;startup=Task.Run(async()=>{if(failed)await StartAsync();else {await viewModel.StopAsync(stopping.Token);await viewModel.InitializeAsync(stopping.Token);}Interlocked.Increment(ref ChangeVersion);});}
  else if(action==13&&startup.IsCompleted)startup=Task.Run(async()=>{await viewModel.LoadMoreWorkflowsAsync(stopping.Token);Interlocked.Increment(ref ChangeVersion);});
  else if(action>=10&&action<=12){Volatile.Write(ref selectedTimeFrame,(int)action-9);if(startup.IsCompletedSuccessfully)viewModel.SelectedTimeFrame=(TimeFrameType)(action-9);}
  else if(action>=1000&&selections.TryGetValue(action,out var identity))viewModel.SelectWorkflow(identity);
 }
 public DetailSnapshot Snapshot(StrategyDetailsAdapter adapter,long generation,string epoch)
 {
  if(!startup.IsCompletedSuccessfully)return new DetailSnapshot(1,epoch,generation,"Live","",0,[]) with{Status=Status};
  var rows=viewModel.Workflows;
  if(startup.IsCompletedSuccessfully&&viewModel.SelectedWorkflowDetails is null&&rows.Count>0)viewModel.SelectWorkflow(rows[0].WorkflowId);
  var details=viewModel.SelectedWorkflowDetails;
  var snapshot=details is null?new DetailSnapshot(1,epoch,generation,"Live","",0,[]):adapter.Project(details,generation);
  var window=viewModel.SelectedGraphWindow;
  return snapshot with {Mode="Live",Status=Status,ContractId=viewModel.ContractId,TimeFrame=viewModel.SelectedTimeFrame.ToString(),GraphStartMarket=StrategyDetailsAdapter.MarketSeconds(window.StartUtc),GraphEndMarket=StrategyDetailsAdapter.MarketSeconds(window.EndUtc),Rows=rows.Select(r=>StrategyDetailsAdapter.Display(r,Token(r.WorkflowId))).ToArray(),Signals=viewModel.Events.OrderBy(r=>r.OccurredOn).ThenBy(r=>r.SequenceId).Select(StrategyDetailsAdapter.Graph).ToArray(),GraphStartUtc=new DateTimeOffset(window.StartUtc).ToUnixTimeSeconds(),GraphEndUtc=new DateTimeOffset(window.EndUtc).ToUnixTimeSeconds(),PageNumber=viewModel.WorkflowPageNumber,PageCount=viewModel.WorkflowPageCount,TotalRows=viewModel.WorkflowTotalCount,HasMore=viewModel.HasMoreWorkflows,Workflows=rows.Select(r=>new WorkflowChoice(r.WorkflowId.ToString(),$"{r.OccurredOn:HH:mm:ss} {r.Outcome} {r.WorkflowId}",Token(r.WorkflowId))).ToArray()};
 }
 uint Token(TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId identity){if(tokens.TryGetValue(identity,out var token))return token;if(tokens.Count>=10000){tokens.Clear();selections.Clear();}token=nextSelection++;tokens[identity]=token;selections[token]=identity;return token;}
 public void Dispose(){stopping.Cancel();try{startup.GetAwaiter().GetResult();}catch(Exception)when(startup.IsCanceled||startup.IsFaulted){}viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();services.DisposeAsync().AsTask().GetAwaiter().GetResult();stopping.Dispose();}
}
