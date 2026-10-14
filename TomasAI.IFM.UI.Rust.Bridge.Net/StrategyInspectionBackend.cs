using System.Globalization;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.UI.Net.ViewModels.Strategy;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed record InspectionIntent(int Schema,Guid RequestId,string View,string Intent,Dictionary<string,string> Fields);
public sealed record InspectionRow(string Id,string Label,Dictionary<string,string> Fields);
public sealed record InspectionResult(string View,InspectionRow[] Rows,DetailSnapshot? Detail=null,string Decision="",string Evidence="",string? PagingState=null,Dictionary<string,string>? Context=null);
/// <summary>Read-only native Strategy Observation and Risk History operations using the same domain query contracts.</summary>
public sealed class StrategyInspectionBackend : IDisposable
{
 readonly RustClientServiceScope? scope;
 readonly IMarketConditionAssessmentQueryApi assessments;
 readonly IIntrinsicTimeStrategyWorkflowQueryApi workflows;
 readonly IRiskQueryApi risk;
 readonly Task ready;
 readonly StrategyDetailsAdapter adapter=new();
 readonly CancellationTokenSource stopping=new();
 public StrategyInspectionBackend(){scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");ready=scope.StartAsync(stopping.Token).AsTask();assessments=scope.Container.GetInstance<IMarketConditionAssessmentQueryApi>();workflows=scope.Container.GetInstance<IIntrinsicTimeStrategyWorkflowQueryApi>();risk=scope.Container.GetInstance<IRiskQueryApi>();}
 public StrategyInspectionBackend(IMarketConditionAssessmentQueryApi assessments,IIntrinsicTimeStrategyWorkflowQueryApi workflows,IRiskQueryApi risk){this.assessments=assessments;this.workflows=workflows;this.risk=risk;ready=Task.CompletedTask;}
 static string Field(InspectionIntent input,string name)=>input.Fields.GetValueOrDefault(name)?.Trim()??"";
 static Guid Identity(InspectionIntent input,string name)=>Guid.TryParse(Field(input,name),out var id)&&id!=Guid.Empty?id:throw new ArgumentException("Enter a valid "+name);
 static int Number(InspectionIntent input,string name)=>int.TryParse(Field(input,name),out var id)&&id>0?id:throw new ArgumentException("Enter a positive "+name);
 public async Task<InspectionResult> ExecuteAsync(InspectionIntent input)
 {
  await ready;
  if(input.View=="observation")
  {
   if(input.Intent=="history")
   {
    if(!Enum.TryParse<TimeFrameType>(Field(input,"horizon"),out var horizon)||horizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))throw new ArgumentException("Select Daily, Weekly or Monthly");
    var profile=Field(input,"profile");if(profile.Length==0)throw new ArgumentException("Enter a market profile");
    var result=await assessments.HistoryAsync(profile,"ES",horizon,DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc),25,stopping.Token);
    if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
    var rows=(result.Value??[]).Select(item=>{var value=MarketConditionAssessmentContracts.ReadResult(item.Result!);return new InspectionRow(item.WorkflowId.ToString(),$"{value.EvaluatedAtUtc:yyyy-MM-dd HH:mm:ss} {value.TargetHorizon} {value.Assessment.Availability} {value.Assessment.ConditionType}",new(){["workflow"]=item.WorkflowId.Value.ToString("D")});}).ToArray();
    return new("observation",rows);
   }
   var workflowId=new StrategyWorkflowId(Identity(input,"workflow"));
   var workflow=await workflows.GetByIdAsync(workflowId);
   if(!workflow.Success||workflow.Value is null)throw new InvalidOperationException(workflow.ErrorMessage);
   var view=MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(workflow.Value.StatePayload);
   TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events.MarketConditionAssessmentCompletedEvent? projected=null;
   if(view.AssessmentBinding is not null){var result=await assessments.GetAsync(view.WorkflowId,stopping.Token);if(result.Success)projected=result.Value;}
   var comparison=MarketAssessmentPresenter.Render(view,projected,DateTime.UtcNow);
   Dictionary<string,string>? context=null;
   if(view.CurrentStage==StrategyWorkflowStage.RiskManagement&&view.OrderComposition is {Result:{CompositionResult:{Candidate:{ } candidate}}})context=new(){["portfolio"]=candidate.PortfolioId.ToString(CultureInfo.InvariantCulture),["fund"]=candidate.FundId.ToString(CultureInfo.InvariantCulture),["workflow"]=view.WorkflowId.Value.ToString("D"),["invocation"]=(view.RiskExecution?.CommandId??view.CausationId).ToString("D")};
   return new("observation",[],adapter.Project(StrategyWorkflowPresentation.CreateDetails(view,comparison),1) with{Mode="Live"},Context:context);
  }
  if(input.Intent is "history" or "next")
  {
   if(!DateOnly.TryParseExact(Field(input,"date"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new ArgumentException("Enter date as yyyy-MM-dd (UTC)");
   var response=await risk.GetHistoryAsync(Number(input,"portfolio"),Number(input,"fund"),date,25,input.Intent=="next"?Field(input,"cursor"):null,stopping.Token);
   if(!response.Success||response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
   return new("risk",response.Value.Items.Select(row=>new InspectionRow(row.InvocationId.ToString("D"),$"{row.EvaluatedAtUtc:yyyy-MM-dd HH:mm:ss} {row.Horizon} {row.Variant} {row.Outcome} {row.Units} {row.Reason}",new(){["workflow"]=row.WorkflowId.ToString("D"),["invocation"]=row.InvocationId.ToString("D")})).ToArray(),PagingState:response.Value.PagingState);
  }
  var observed=await risk.GetInvocationAsync(new(Identity(input,"workflow")),Identity(input,"invocation"),stopping.Token);
  if(!observed.Success||observed.Value is null)throw new InvalidOperationException(observed.ErrorMessage);
  return new("risk",[],Decision:RiskObservationPresentation.Render(observed.Value),Evidence:JsonSerializer.Serialize(observed.Value,new JsonSerializerOptions{WriteIndented=true}));
 }
 public void Dispose(){stopping.Cancel();scope?.DisposeAsync().AsTask().GetAwaiter().GetResult();adapter.Dispose();stopping.Dispose();}
}
