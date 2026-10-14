using System.Text.Json;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed record ContractEditorIntent(int Schema, Guid RequestId, string View, string Intent,
    long ExpectedGeneration, string? OriginalId, Dictionary<string,string> Fields);
public sealed record ContractEditorRow(string Id, Dictionary<string,string> Fields);
public sealed record ContractEditorResult(string View, ContractEditorRow[] Rows,
    Dictionary<string,string[]> Choices, bool HasMore = false, Dictionary<string,string>? Context = null);
public sealed record ContractEditorPublication(int Schema, long Generation, string Status, bool Busy,
    ContractEditorResult Result, EditorOutcome[] Outcomes);
/// <summary>Explicit contract editing boundary with immutable snapshots and one correlated operation at a time.</summary>
public sealed class ContractEditorGateway(Func<ContractEditorIntent,Task<ContractEditorResult>> execute, Func<ContractEditorIntent,bool>? validate)
{
    public ContractEditorGateway(Func<ContractEditorIntent,Task<ContractEditorResult>> execute) : this(execute,null) {}
    public static bool SupportsView(string? view) => view is "futures-contract" or "option-contract" or "yield-curve" or "economic-calendar" or "provider-definitions" or "scheduled-tasks" or "operations-health" or "actor-health" or "parameter-sets" or "strategy-catalog";
    readonly object gate = new();
    readonly Dictionary<Guid,(string Payload,EditorOutcome Outcome)> requests = [];
    readonly Queue<Guid> completedReadRefreshes = new();
    ContractEditorResult result = new("",[],[]);
    long generation = 1;
    bool busy;
    string status = "Not loaded";
    Task pending = Task.CompletedTask;
    public ContractEditorPublication Snapshot()
    {
        lock(gate) return new(1,generation,status,busy,result with{Context=result.Context??[]},requests.Values.Select(x=>x.Outcome).ToArray());
    }
    public int Submit(ContractEditorIntent input)
    {
        if(validate is not null ? !validate(input) : (input.Schema != 1 || input.RequestId == Guid.Empty || !SupportsView(input.View)
           || !ParameterSetSession.Actions.Contains(input.Intent) && !StrategyCatalogSession.Actions.Contains(input.Intent) && input.Intent is not ("load" or "next" or "add" or "change" or "remove" or "range" or "import" or "review" or "preview" or "publish" or "history" or "more-output" or "output" or "enable" or "disable" or "run" or "resolve" or "supervise")
           || (input.Intent=="review" && input.View is not ("futures-contract" or "option-contract"))
           || (input.View=="operations-health" && input.Intent!="load")
           || (input.View=="actor-health" && input.Intent is not ("load" or "supervise"))
           || (input.Intent=="supervise" && input.View!="actor-health")
           || (input.View=="parameter-sets" && !ParameterSetSession.Actions.Contains(input.Intent))
           || (input.View!="parameter-sets" && ParameterSetSession.Actions.Contains(input.Intent) && input.Intent!="load")
           || (input.View=="strategy-catalog" && !StrategyCatalogSession.Actions.Contains(input.Intent))
           || (input.View!="strategy-catalog" && StrategyCatalogSession.Actions.Contains(input.Intent) && input.Intent!="load")
           || input.Fields is null || input.Fields.Count > 64
           || input.Fields.Any(x=>x.Key.Length>64 || x.Value is null || x.Value.Length>(input.View is "parameter-sets" or "strategy-catalog"?131072:2048))
           || (input.Intent is ("range" or "import") && input.View is not ("yield-curve" or "economic-calendar"))
           || (input.Intent=="publish" && input.View!="provider-definitions") || (input.Intent=="preview" && input.View is not ("provider-definitions" or "scheduled-tasks")) || (input.Intent is ("history" or "more-output" or "output" or "enable" or "disable" or "run" or "resolve") && input.View!="scheduled-tasks") || input.OriginalId?.Length>512 || (input.View=="provider-definitions" && input.Intent is not ("load" or "next" or "preview" or "publish")) || (input.Intent=="next" && input.View is not ("option-contract" or "provider-definitions" or "scheduled-tasks")))) return -1;
        input = input with { Fields = new(input.Fields,StringComparer.Ordinal) };
        var payload = JsonSerializer.Serialize(new{input.View,input.Intent,input.ExpectedGeneration,input.OriginalId,
            Fields=input.Fields.OrderBy(x=>x.Key,StringComparer.Ordinal)});
        lock(gate)
        {
            if(requests.TryGetValue(input.RequestId,out var previous)) return previous.Payload==payload?0:-1;
            if(input.View=="parameter-sets" && input.Intent is not ("load" or "resume-parameter") && (input.ExpectedGeneration!=generation || result.View!="parameter-sets"))return -5;
            if(input.View=="parameter-sets" && input.Intent=="select" && !result.Rows.Any(x=>x.Id==input.OriginalId))return -5;
            if(input.View=="parameter-sets" && input.Intent is not ("load" or "resume-parameter" or "component" or "select") && (input.OriginalId??"")!=result.Context?.GetValueOrDefault("selectedId"))return -5;
            if(input.View=="strategy-catalog" && input.Intent is not ("load" or "catalog-resume") && (input.ExpectedGeneration!=generation || result.View!="strategy-catalog"))return -5;
            if(input.View=="strategy-catalog" && input.Intent=="catalog-select" && !result.Rows.Any(x=>x.Id==input.OriginalId))return -5;
            if(input.View=="strategy-catalog" && input.Intent is not ("load" or "catalog-resume" or "catalog-section" or "catalog-select") && (input.OriginalId??"")!=result.Context?.GetValueOrDefault("selectedId"))return -5;
            if(validate is not null && input.Intent != "load" && (input.ExpectedGeneration != generation || result.View != input.View))return -5;
            if(busy || requests.Count>=256) return -4;
            if(input.Intent is "add" or "change" or "remove" or "review" or "enable" or "disable" or "run" or "resolve" or "history" or "output" or "more-output" or "supervise" || input.View=="scheduled-tasks" && input.Intent=="preview")
            {
                if(input.ExpectedGeneration != generation || result.View != input.View) return -5;
                if(input.Intent is ("change" or "remove" or "review" or "enable" or "disable" or "run" or "resolve" or "history" or "output" or "more-output" or "supervise") && !result.Rows.Any(x=>x.Id==input.OriginalId)) return -5;
            }
            if(input.Intent is ("range" or "import") && (result.View!=input.View||input.ExpectedGeneration!=generation))return -5;
            if(input.View=="provider-definitions" && input.Intent is ("preview" or "publish") && (result.View!=input.View || input.ExpectedGeneration!=generation || !result.Rows.Any(x=>x.Id==input.OriginalId)))return -5;
            if(input.Intent=="publish" && (!result.Context?.TryGetValue("previewToken",out var token) ?? true))return -5;
            if(input.Intent=="publish" && (!input.Fields.TryGetValue("previewToken",out var supplied) || supplied!=result.Context!["previewToken"] || input.OriginalId!=result.Context.GetValueOrDefault("previewSelectedId")))return -5;
            if(input.Intent=="next" && (result.View!=input.View || !result.HasMore || input.ExpectedGeneration!=generation)) return -5;
            requests.Add(input.RequestId,(payload,new(input.RequestId,"Pending","Awaiting backend completion")));
            busy=true;status="Pending";generation++;
            pending=Task.Run(async()=>
            {
                try
                {
                    var loaded=await execute(input);
                    lock(gate){result=loaded;status="Completed";requests[input.RequestId]=(payload,new(input.RequestId,"Completed","Backend completion received"));
                        RetainReadRefreshOutcome(input);
                        busy=false;generation++;}
                }
                catch(Exception error)
                {
                    lock(gate){if((error is TimeoutException or EditorOutcomeUnknownException) && !IsReadRefresh(input))result=result with{View=""};status=error.Message;requests[input.RequestId]=(payload,new(input.RequestId,
                        error is TimeoutException or EditorOutcomeUnknownException ? "Unknown" : "Failed",error.Message));RetainReadRefreshOutcome(input);busy=false;generation++;}
                }
            });
            return 0;
        }
    }
    static bool IsReadRefresh(ContractEditorIntent input)
        => input.View=="trade-orders" && input.Intent is "refresh-chain" or "refresh-execution";
    // Only idempotent polling outcomes expire; mutation request IDs remain protected.
    void RetainReadRefreshOutcome(ContractEditorIntent input)
    {
        if(!IsReadRefresh(input))return;
        completedReadRefreshes.Enqueue(input.RequestId);
        while(completedReadRefreshes.Count>8)requests.Remove(completedReadRefreshes.Dequeue());
    }
    /// <summary>Refreshes an observed view from notifications without adding synthetic user actions.</summary>
    public bool TryRefresh(string view, Func<Task<ContractEditorResult>> refresh)
    {
        lock(gate)
        {
            if(busy || result.View!=view)return false;
            busy=true;generation++;
            pending=Task.Run(async()=>
            {
                try {var loaded=await refresh();lock(gate){result=loaded;status="Refreshed";busy=false;generation++;}}
                catch(Exception e){lock(gate){status=e.Message;busy=false;generation++;}}
            });
            return true;
        }
    }
    public Task DrainAsync(){lock(gate)return pending;}
}
