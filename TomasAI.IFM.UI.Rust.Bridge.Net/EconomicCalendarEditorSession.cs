using System.Globalization;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Economic calendar adapter with existing view-model command correlation; filter dates are Eastern, event instants are UTC.</summary>
sealed class EconomicCalendarEditorSession:IDisposable
{
 readonly RustClientServiceScope scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");
 readonly EconomicCalendarEditorViewModel model;
 readonly Task ready;
 readonly CancellationTokenSource stopping=new();
 DateOnly filterDate;string country="US";
 public EconomicCalendarEditorSession(){model=new(scope,scope.Container.GetInstance<IEconomicCalendarService>());ready=scope.StartAsync(stopping.Token).AsTask();}
 static string Field(ContractEditorIntent input,string name)=>input.Fields.GetValueOrDefault(name)?.Trim()??"";
 static string? Optional(ContractEditorIntent input,string name)=>Field(input,name) is {Length:>0} value?value:null;
 static string Key(EconomicCalendarUiModel row)=>Convert.ToBase64String(BridgeDocument.Encode(new{row.EventDate,row.CountryCode,row.EventName}));
 public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
 {
  await ready;if(Field(input,"filterDate") is {Length:>0} filter && !DateOnly.TryParseExact(filter,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ArgumentException("Enter the filter date as yyyy-MM-dd");filterDate=DateOnly.TryParseExact(Field(input,"filterDate"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:scope.ValueDates.ValueDate;
  if(input.Intent=="load"){
   await Observe(model.LoadCountryCodes());
   if(model.CountryCodes.Count==0)throw new InvalidOperationException("No economic-calendar countries are published");
   country=Field(input,"filterCountry") is {Length:>0} selected?selected:model.CountryCodes.Any(x=>x.CountryCode=="US")?"US":model.CountryCodes.First().CountryCode;
   ValidateCountry();await Observe(model.LoadEconomicCalendars(filterDate,country));
  }else if(input.Intent=="range"){
   country=Field(input,"filterCountry");ValidateCountry();await Observe(model.LoadEconomicCalendars(filterDate,country));
  }else if(input.Intent=="import"){
   ValidateCountry();model.PrepareImport(filterDate.ToDateTime(TimeOnly.MinValue),country);await Observe(model.ImportOperation.ExecuteAsync(stopping.Token));
  }else{
   var original=model.EconomicCalendars.SingleOrDefault(x=>Key(x)==input.OriginalId);
   if(input.Intent=="remove"){
    if(original is null)throw new InvalidOperationException("The selected calendar is no longer loaded");
    await Observe(model.RemoveEconomicCalendar(original.EventDate,original.CountryCode,original.EventName,true));
   }else{
    if(!DateTimeOffset.TryParseExact(Field(input,"eventDate"),"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var instant)||instant.Offset!=TimeSpan.Zero)throw new ArgumentException("Enter the event instant as an ISO UTC timestamp");
    var row=new EconomicCalendarUiModel(instant.UtcDateTime,Field(input,"countryCode"),Field(input,"eventName"),Optional(input,"actual"),Optional(input,"forecast"),Optional(input,"prior"),original?.CreatedOn??DateTime.UtcNow,original?.CreatedBy??"Rust UI",Optional(input,"impact"),Optional(input,"unit"),Optional(input,"change"),Optional(input,"changePercentage"));
    if(!row.IsValid||row.EventName.Length==0||!model.CountryCodes.Any(x=>x.CountryCode==row.CountryCode))throw new ArgumentException("Select a published country and supply an event name/date");
    if(input.Intent=="add")await Observe(model.AddEconomicCalendar(row,()=>{}));
    else{if(original is null)throw new InvalidOperationException("The selected calendar is no longer loaded");await Observe(model.ChangeEconomicCalendar(original.EventDate,original.CountryCode,original.EventName,row,true,()=>{}));}
    country=row.CountryCode;filterDate=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(row.EventDate,TimeZoneInfo.FindSystemTimeZoneById("America/New_York")));
   }
  }
  return new(input.View,model.EconomicCalendars.Select(row=>new ContractEditorRow(Key(row),new(){
   ["eventDate"]=DateTime.SpecifyKind(row.EventDate,DateTimeKind.Utc).ToString("O",CultureInfo.InvariantCulture),["countryCode"]=row.CountryCode,["eventName"]=row.EventName,["actual"]=row.Actual??"",["forecast"]=row.Forecast??"",["prior"]=row.Prior??"",["impact"]=row.Impact??"",["unit"]=row.Unit??"",["change"]=row.Change??"",["changePercentage"]=row.ChangePercentage??"",["createdOn"]=row.CreatedOn.ToString("O",CultureInfo.InvariantCulture),["createdBy"]=row.CreatedBy})).ToArray(),new(){["countryCode"]=model.CountryCodes.Select(x=>x.CountryCode).ToArray()},Context:new(){["filterDate"]=filterDate.ToString("yyyy-MM-dd"),["filterCountry"]=country,["newEventUtc"]=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture)});
 }
 void ValidateCountry(){if(!model.CountryCodes.Any(x=>x.CountryCode==country))throw new ArgumentException("Select a published country");}
 async Task Observe(Task operation){try{await operation.WaitAsync(TimeSpan.FromSeconds(30),stopping.Token);}catch(TimeoutException){await model.StopAsync(CancellationToken.None);try{await operation;}catch(OperationCanceledException){}throw new EditorOutcomeUnknownException("Completion was not observed. Reload calendars before retrying.");}}
 public void Dispose(){stopping.Cancel();model.DisposeAsync().AsTask().GetAwaiter().GetResult();scope.DisposeAsync().AsTask().GetAwaiter().GetResult();stopping.Dispose();}
}
