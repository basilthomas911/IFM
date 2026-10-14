using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Owns catalog working copies; domain APIs validate and persist immutable versions.</summary>
public sealed class StrategyCatalogSession : IAsyncDisposable
{
 public static readonly string[] Actions=["load","catalog-section","catalog-select","catalog-new","catalog-edit","catalog-discard","catalog-save","catalog-publish","catalog-retire","catalog-resume"];
 readonly RustClientServiceScope? scope; readonly IReferenceQueryApi queries; readonly IReferenceCommandApi commands;
 readonly List<StrategyCatalogSummary> references=[];CatalogProduct[] products=[]; StoredStrategyCatalogDefinition? selected; StrategyCatalogDefinition? draft;
 StrategyCatalogKind kind=StrategyCatalogKind.Family; bool started,loaded,showAll; CatalogCommandRequest? retry;
 static readonly HashSet<(StrategyCatalogKind,Guid)> defaults=StrategyCatalogDefaults.Create().Select(x=>(x.Key.Kind,x.Key.Id)).ToHashSet();
 public StrategyCatalogSession(){scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");queries=scope.Container.GetInstance<IReferenceQueryApi>();commands=scope.Container.GetInstance<IReferenceCommandApi>();}
 public StrategyCatalogSession(IReferenceQueryApi queries,IReferenceCommandApi commands){this.queries=queries;this.commands=commands;}
 public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
 {
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));var ct=deadline.Token;
  if(scope is not null&&!started){await scope.StartAsync(ct);started=true;}
  if(!loaded&&input.Intent!="load")throw new InvalidOperationException("Load the catalog first.");
  if(draft is not null&&input.Intent is "load" or "catalog-section" or "catalog-select" or "catalog-new" or "catalog-edit")throw new InvalidOperationException("Save or discard the working copy first.");
  switch(input.Intent){
   case "load":await Load(ct);selected=null;break;
   case "catalog-resume":break;
   case "catalog-section":kind=Enum.Parse<StrategyCatalogKind>(input.Fields["kind"]);if(!Enum.IsDefined(kind))throw new ArgumentException("Unknown catalog section.");showAll=input.Fields.GetValueOrDefault("showAll")=="true";selected=null;break;
   case "catalog-select":selected=await Exact(references.Single(x=>Id(x.Key)==input.OriginalId).Key,ct);break;
   case "catalog-new":var id=Guid.NewGuid();var template=StrategyCatalogDefaults.Create().FirstOrDefault(x=>Id(x.Key)==input.Fields.GetValueOrDefault("template"));if(template is not null&&template.Key.Kind!=kind)throw new ArgumentException("Template belongs to another section.");draft=template is not null?template with{Key=new(kind,id,1),Code=template.Code+"-"+id.ToString("N")[..6]}:new(){Key=new(kind,id,1),Code="",Name="",Horizon=kind==StrategyCatalogKind.Deployment?TimeFrameType.Daily:default,Side=kind==StrategyCatalogKind.Variant?"Long":"",Bias=kind==StrategyCatalogKind.Variant?"Balanced":"",PremiumMode=kind==StrategyCatalogKind.Variant?"None":""};break;
   case "catalog-edit":if(selected is null)throw new InvalidOperationException("Select an exact version.");draft=selected.Definition with{Key=selected.Definition.Key with{Version=checked(selected.Definition.Key.Version+1)}};break;
   case "catalog-discard":draft=null;retry=null;break;
   case "catalog-save":if(draft is null)throw new InvalidOperationException("Open a working copy first.");var supplied=StrategyCatalogJson.Read<StrategyCatalogDefinition>(input.Fields["definition"]);if(supplied.Key!=draft.Key||supplied.Key.Version>1&&supplied.Code!=draft.Code)throw new ArgumentException("Catalog identity and existing code cannot change.");if(string.IsNullOrWhiteSpace(supplied.Code)||string.IsNullOrWhiteSpace(supplied.Name))throw new ArgumentException("Code and Name are required.");draft=supplied;await Send(new(input.RequestId,CatalogCommandOperation.SaveDraft,Definition:supplied,ExpectedPreviousVersion:supplied.Key.Version-1),ct);selected=await Confirm(supplied.Key,CatalogLifecycleStatus.Draft,ct);draft=null;showAll=true;await Load(ct);break;
   case "catalog-publish":case "catalog-retire":if(draft is not null||selected is null)throw new InvalidOperationException("Select a saved version first.");var retire=input.Intent=="catalog-retire";if(selected.Status!=(retire?CatalogLifecycleStatus.Published:CatalogLifecycleStatus.Draft))throw new InvalidOperationException("Selected lifecycle state does not allow this action.");var key=selected.Definition.Key;await Send(new(input.RequestId,retire?CatalogCommandOperation.Retire:CatalogCommandOperation.Publish,Key:key,ExpectedHash:selected.ContentHash,EffectiveUtc:new DateTime(DateTime.UtcNow.Ticks/10*10,DateTimeKind.Utc)),ct);selected=await Confirm(key,retire?CatalogLifecycleStatus.Retired:CatalogLifecycleStatus.Published,ct);await Load(ct);break;
   default:throw new ArgumentException("Unsupported catalog action.");
  }return Snapshot();
 }
 async Task Load(CancellationToken ct)
 {
  var all=new List<StrategyCatalogSummary>();foreach(var section in Enum.GetValues<StrategyCatalogKind>()){string? cursor=null;while(true){var page=StrategyCatalogJson.Read<StrategyCatalogSummary[]>(await Query(new(CatalogQueryOperation.List,section,Limit:100,AfterCode:cursor),ct));all.AddRange(page);if(page.Length<100)break;if(page[^1].Code==cursor||all.Count>=4096)throw new InvalidOperationException("Catalog pagination failed to advance or exceeds the editor limit.");cursor=page[^1].Code;}}
  var rows=new List<CatalogProduct>();foreach(var family in new[]{TradeStrategyFamilyType.Futures,TradeStrategyFamilyType.FuturesOption}){var result=await queries.GetTradeStrategySymbolsAsync(family,ct);if(!result.Success||result.Value is null)throw new InvalidOperationException(result.ErrorMessage??"Product catalog unavailable.");rows.AddRange(result.Value.Select(x=>new CatalogProduct(x.Id,x.Symbol,x.Exchange,x.Currency)));}
  references.Clear();references.AddRange(all);products=rows.Distinct().ToArray();loaded=true;
 }
 async Task<string> Query(CatalogQueryRequest request,CancellationToken ct){var result=await queries.QueryStrategyCatalogAsync(request,ct);return result.Success&&result.Value is not null?result.Value:throw new InvalidOperationException(result.ErrorMessage??"Catalog query failed.");}
 async Task<StoredStrategyCatalogDefinition> Exact(CatalogKey key,CancellationToken ct){var value=StrategyCatalogJson.Read<StoredStrategyCatalogDefinition>(await Query(new(CatalogQueryOperation.Exact,Key:key),ct));return value.Definition.Key==key?value:throw new InvalidOperationException("Query returned another version.");}
 async Task<StoredStrategyCatalogDefinition> Confirm(CatalogKey key,CatalogLifecycleStatus expected,CancellationToken ct){try{var value=await Exact(key,ct);return value.Status==expected?value:throw new InvalidOperationException("Lifecycle state was not verified.");}catch(Exception e){throw new EditorOutcomeUnknownException("Catalog command completed but authoritative verification failed: "+e.Message);}}
 async Task Send(CatalogCommandRequest request,CancellationToken ct){if(retry is not null&&StrategyCatalogJson.Write(retry with{OperationId=Guid.Empty})==StrategyCatalogJson.Write(request with{OperationId=Guid.Empty}))request=retry;retry=request;var result=await commands.ExecuteStrategyCatalogAsync(request,ct);if(!result.Success)throw new InvalidOperationException(result.ErrorMessage??"Catalog command failed.");retry=null;}
 ContractEditorResult Snapshot()=>new("strategy-catalog",references.Where(x=>x.Key.Kind==kind&&(showAll||defaults.Contains((x.Key.Kind,x.Key.Id))||kind==StrategyCatalogKind.Deployment&&x.Code.StartsWith("Legacy-",StringComparison.Ordinal))).OrderBy(StrategyCatalogDefaults.DisplayOrder).ThenBy(x=>x.Name).Select(x=>new ContractEditorRow(Id(x.Key),new(){["name"]=x.Name,["code"]=x.Code,["version"]=x.Key.Version.ToString(),["status"]=x.Status.ToString()})).ToArray(),new(){["kind"]=Enum.GetNames<StrategyCatalogKind>()},Context:new(){["kind"]=kind.ToString(),["showAll"]=showAll.ToString(),["selectedId"]=selected is null?"":Id(selected.Definition.Key),["editing"]=(draft is not null).ToString(),["definition"]=draft is not null?StrategyCatalogJson.Write(draft):selected is null?"null":StrategyCatalogJson.Write(selected.Definition),["references"]=StrategyCatalogJson.Write(references),["products"]=StrategyCatalogJson.Write(products),["horizons"]=StrategyCatalogJson.Write(new[]{TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly}.Select(x=>new{Name=x.ToString(),Value=(int)x})),["templates"]=StrategyCatalogJson.Write(StrategyCatalogDefaults.Create().Where(x=>x.Key.Kind==kind).ToArray())});
 public static string Id(CatalogKey key)=>$"{key.Kind}:{key.Id:D}:{key.Version}";
 public async ValueTask DisposeAsync(){if(scope is not null)await scope.DisposeAsync();}
}
