using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Typed yield editor adapter; validates duplicate dates and awaits existing correlated command completion.</summary>
sealed class YieldCurveEditorSession:IDisposable
{
 readonly RustClientServiceScope scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");
 readonly YieldCurveRateEditorViewModel model;
 readonly YieldCurveRateEditViewModel validation;
 readonly Task ready;
 readonly CancellationTokenSource stopping=new();
 public YieldCurveEditorSession(){model=new(scope);validation=new(scope);ready=scope.StartAsync(stopping.Token).AsTask();}
 static string Field(ContractEditorIntent input,string name)=>input.Fields.GetValueOrDefault(name)?.Trim()??"";
 static DateOnly Date(ContractEditorIntent input,string name)=>DateOnly.ParseExact(Field(input,name),"yyyy-MM-dd",CultureInfo.InvariantCulture);
 static double Rate(ContractEditorIntent input,string name){var value=double.Parse(Field(input,name),CultureInfo.InvariantCulture);return double.IsFinite(value)?value:throw new ArgumentException("Rates must be finite");}
 public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
 {
  await ready;
  switch(input.Intent)
  {
   case "load":await Run(model.LoadOperation);break;
   case "range":
    var index=model.TimePeriods.ToList().FindIndex(x=>x==Field(input,"timePeriod"));if(index<0)throw new ArgumentException("Select a published time period");
    model.SelectTimePeriod(index,scope.ValueDates.ValueDate);await Run(model.LoadRatesOperation);break;
   case "import":model.PrepareImport(Date(input,"importDate").ToDateTime(TimeOnly.MinValue));await Run(model.ImportOperation);break;
   case "remove":model.PrepareRemove(model.YieldCurveRates.Single(x=>x.ValueDate.ToString("yyyy-MM-dd")==input.OriginalId));await Run(model.RemoveOperation);break;
   default:
    var date=Date(input,"valueDate");
    if(input.Intent=="change" && input.OriginalId!=date.ToString("yyyy-MM-dd"))throw new ArgumentException("A saved yield curve's value date cannot change");
    var row=new YieldCurveRateReadModel(date,Rate(input,"oneMonth"),Rate(input,"twoMonth"),Rate(input,"threeMonth"),Rate(input,"sixMonth"),Rate(input,"oneYear"),Rate(input,"twoYear"),Rate(input,"threeYear"),Rate(input,"fiveYear"),Rate(input,"sevenYear"),Rate(input,"tenYear"),Rate(input,"twentyYear"),Rate(input,"thirtyYear"));
    if(input.Intent=="add"){
     validation.SetValueDate(date);await Run(validation.CheckValueDateOperation);
     if(!validation.CanSave)throw new ArgumentException("A yield curve already exists for this value date");
     model.PrepareAdd(row);await Run(model.AddOperation);
    }else{model.PrepareChange(row);await Run(model.ChangeOperation);}break;
  }
  return new(input.View,model.YieldCurveRates.Select(row=>new ContractEditorRow(row.ValueDate.ToString("yyyy-MM-dd"),new(){
   ["valueDate"]=row.ValueDate.ToString("yyyy-MM-dd"),["oneMonth"]=Format(row.OneMonth),["twoMonth"]=Format(row.TwoMonth),["threeMonth"]=Format(row.ThreeMonth),["sixMonth"]=Format(row.SixMonth),["oneYear"]=Format(row.OneYear),["twoYear"]=Format(row.TwoYear),["threeYear"]=Format(row.ThreeYear),["fiveYear"]=Format(row.FiveYear),["sevenYear"]=Format(row.SevenYear),["tenYear"]=Format(row.TenYear),["twentyYear"]=Format(row.TwentyYear),["thirtyYear"]=Format(row.ThirtyYear)})).ToArray(),
   new(){["timePeriod"]=model.TimePeriods.ToArray()},Context:new(){["timePeriod"]=model.SelectedTimePeriod,["valueDate"]=scope.ValueDates.ValueDate.ToString("yyyy-MM-dd"),["rangeStart"]=model.RangeStart.ToString("yyyy-MM-dd"),["rangeEnd"]=model.RangeEnd.ToString("yyyy-MM-dd")});
 }
 static string Format(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
 async Task Run(IAsyncOperation operation)
 {
  if(!operation.CanExecute)throw new InvalidOperationException("The existing yield curve operation is unavailable");
  var pending=operation.ExecuteAsync(stopping.Token);
  try{await pending.WaitAsync(TimeSpan.FromSeconds(30),stopping.Token);}
  catch(TimeoutException){operation.Cancel();try{await pending;}catch(OperationCanceledException){}throw new EditorOutcomeUnknownException("Completion was not observed. Reload the yield curve before retrying.");}
 }
 public void Dispose(){stopping.Cancel();model.DisposeAsync().AsTask().GetAwaiter().GetResult();scope.DisposeAsync().AsTask().GetAwaiter().GetResult();stopping.Dispose();}
}
