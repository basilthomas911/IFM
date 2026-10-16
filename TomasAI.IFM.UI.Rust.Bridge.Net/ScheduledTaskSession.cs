using System.Globalization;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Adapts the shared scheduled-task view model without installing or executing tasks in the renderer.</summary>
public sealed class ScheduledTaskSession : IDisposable
{
    readonly RustClientServiceScope? scope;
    readonly ScheduledTasksViewModel model;
    readonly Task ready;
    bool initialized;
    int refreshPending;
    public bool RefreshPending => Volatile.Read(ref refreshPending)!=0;
    string? error;
    Guid? historySchedule;
    ScheduledTaskRunUiModel[] runs = [];
    readonly Dictionary<Guid,ScheduledTaskRunUiModel[]> histories=[];
    readonly Dictionary<Guid,string> nextFires=[];
    byte[]? cursor;
    string output = "";
    Guid? outputRun;
    long offset;
    bool outputEnd = true;
    ScheduledTaskPreviewUiModel? preview;
    public ScheduledTaskSession()
    {
        scope = new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
        model = new(scope.Container.GetInstance<IScheduledTaskService>());
        model.Error += message => error = message;
        model.RefreshRequested += () => Interlocked.Exchange(ref refreshPending,1);
        ready = scope.StartAsync().AsTask();
    }
    public ScheduledTaskSession(IScheduledTaskService service)
    { model = new(service); model.Error += message => error = message;
        model.RefreshRequested += () => Interlocked.Exchange(ref refreshPending,1); ready = Task.CompletedTask; }
    static string Field(ContractEditorIntent i, string name) => i.Fields.GetValueOrDefault(name)?.Trim() ?? "";
    static DateTimeOffset? Date(ContractEditorIntent i, string name) => Field(i,name) is { Length: > 0 } text
        ? DateTimeOffset.Parse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind) : null;
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        await ready; error = null;
        if (input.Intent == "load")
        {
            model.Environment = Field(input,"environment"); model.HostId = Field(input,"hostId");
            if (model.Environment.Length == 0 || model.HostId.Length == 0) throw new ArgumentException("Environment and host are required");
            model.SelectedScheduleId = null; historySchedule = null; runs = []; histories.Clear(); nextFires.Clear(); cursor = null; output = ""; preview = null;
            if (!initialized) { await model.InitializeAsync(); initialized = true; } else await model.RefreshAsync();
        }
        else
        {
            if (model.Environment != Field(input,"environment") || model.HostId != Field(input,"hostId"))
                throw new InvalidOperationException("Reload after changing environment or host");
            var selected = model.State.Schedules.SingleOrDefault(x => x.Id.ToString("D") == input.OriginalId);
            switch (input.Intent)
            {
                case "preview": preview = await model.PreviewAsync(ReadSchedule(input)); break;
                case "add": await model.SaveAsync(null,0,ReadSchedule(input)); break;
                case "change": await model.SaveAsync(Required(selected).Id,selected!.Revision,ReadSchedule(input)); break;
                case "enable": await model.SetEnabledAsync(Required(selected).Id,selected!.Revision,true); break;
                case "disable": await model.SetEnabledAsync(Required(selected).Id,selected!.Revision,false); break;
                case "remove":
                    if (Required(selected).Enabled) throw new InvalidOperationException("Disable the schedule before removal");
                    await model.RemoveAsync(selected!.Id,selected.Revision); break;
                case "run": await model.RunNowAsync(Required(selected)); break;
                case "history":
                case "next":
                    Required(selected);
                    if (input.Intent == "next" && (historySchedule != selected!.Id || cursor is null)) throw new InvalidOperationException("No continuation for this schedule");
                    var page = await model.LoadRunHistoryAsync(selected!.Id,input.Intent == "next" ? cursor : null);
                    if (page is not null)
                    {
                        runs = (input.Intent == "next" ? runs.Concat(page.Runs) : page.Runs).DistinctBy(x=>x.Id).OrderBy(x=>x.IntendedFireTimeUtc).ToArray();
                        histories[selected!.Id]=runs;
                        cursor = page.PagingState; historySchedule = selected.Id; output = ""; outputRun = null; offset = 0; outputEnd = true;
                    }
                    break;
                case "output":
                case "more-output":
                case "resolve":
                    if (historySchedule != Required(selected).Id) throw new InvalidOperationException("Load this task's run history first");
                    var run = runs.Single(x=>x.Id.ToString("D") == Field(input,"runId"));
                    if (input.Intent == "resolve")
                    {
                        var reason = Field(input,"reason");
                        if (reason.Length < 3 || run.Status != "Uncertain") throw new ArgumentException("Select an uncertain run and enter a reason");
                        await model.ResolveUncertainAsync(run,reason); break;
                    }
                    if (input.Intent == "more-output" && (outputRun != run.Id || outputEnd)) throw new InvalidOperationException("No output continuation for this run");
                    var chunk = await model.LoadOutputAsync(selected!.Id,run,input.Intent == "more-output" ? offset : 0,CancellationToken.None);
                    if (chunk is not null)
                    {
                        output = (input.Intent == "more-output" ? output : "") + (chunk.Available ? chunk.Text : run.StandardOutputTail + "\n" + run.StandardErrorTail);
                        if (output.Length > 2_000_000) throw new InvalidOperationException("Output view reached 2 MB; reopen a retained output page");
                        outputRun = run.Id; offset = chunk.NextOffset; outputEnd = chunk.EndOfOutput || !chunk.Available;
                    }
                    break;
                default: throw new ArgumentException("Unsupported scheduled-task action");
            }
        }
        if (error is not null) throw new InvalidOperationException(error);
        if(input.Intent is "load" or "add" or "change" or "enable" or "disable") {
            foreach(var definition in model.State.Schedules.Where(x=>!x.Removed)) {
                var timing=await model.PreviewAsync(definition.Schedule);
                nextFires[definition.Id]=timing is {Valid:true,NextFireTimesUtc.Length:>0} ? timing.NextFireTimesUtc[0].ToString("O") : "";
            }
            if(error is not null)throw new InvalidOperationException(error);
        }
        return Publication();
    }
    public async Task<ContractEditorResult> RefreshPublicationAsync()
    {
        Interlocked.Exchange(ref refreshPending,0); error=null; await model.RefreshAsync();
        if(historySchedule is { } id) {var page=await model.LoadRunHistoryAsync(id,null);if(page is not null){runs=page.Runs.Concat(runs).DistinctBy(x=>x.Id).OrderBy(x=>x.IntendedFireTimeUtc).ToArray();histories[id]=runs;}}
        if(error is not null)throw new InvalidOperationException(error);
        return Publication();
    }
    ContractEditorResult Publication()
    {
        var state = model.State;
        return new("scheduled-tasks",state.Schedules.Where(x=>!x.Removed).Select(x=>new ContractEditorRow(x.Id.ToString("D"),Fields(x))).ToArray(),
            new(){["taskKey"]=state.Projects.Where(x=>x.Available).Select(x=>x.TaskKey).ToArray()},
            cursor is not null, new(){["hostStatus"]=state.HostStatus,["output"]=output,
                ["outputRun"]=outputRun?.ToString("D") ?? "",["outputEnd"]=outputEnd.ToString(),["historySchedule"]=historySchedule?.ToString("D") ?? ""
                }){Documents=new(){["histories"]=BridgeDocument.Encode(histories,true),["nextFires"]=BridgeDocument.Encode(nextFires,true),["runs"]=BridgeDocument.Encode(runs,true),["preview"]=BridgeDocument.Encode(preview,true),["projects"]=BridgeDocument.Encode(state.Projects,true)}};
    }
    static ScheduledTaskDefinitionUiModel Required(ScheduledTaskDefinitionUiModel? row) => row is {Removed:false} ? row : throw new InvalidOperationException("Select a current schedule");
    ScheduledTaskScheduleUiModel ReadSchedule(ContractEditorIntent i) => new(Field(i,"taskKey"),Field(i,"name"),model.Environment,model.HostId,
        bool.Parse(Field(i,"oneTime")),Field(i,"expression"),Field(i,"timeZoneId"),Date(i,"startsAtUtc"),Date(i,"endsAtUtc"),
        int.Parse(Field(i,"maximumRuntimeSeconds"),CultureInfo.InvariantCulture),int.Parse(Field(i,"dispatchToleranceSeconds"),CultureInfo.InvariantCulture),Field(i,"description"));
    static Dictionary<string,string> Fields(ScheduledTaskDefinitionUiModel d) => new(){
        ["taskKey"]=d.Schedule.TaskKey,["name"]=d.Schedule.Name,["environment"]=d.Schedule.Environment,["hostId"]=d.Schedule.HostId,
        ["oneTime"]=d.Schedule.OneTime.ToString(),["expression"]=d.Schedule.Expression,["timeZoneId"]=d.Schedule.TimeZoneId,
        ["startsAtUtc"]=d.Schedule.StartsAtUtc?.ToString("O") ?? "",["endsAtUtc"]=d.Schedule.EndsAtUtc?.ToString("O") ?? "",
        ["maximumRuntimeSeconds"]=d.Schedule.MaximumRuntimeSeconds.ToString(CultureInfo.InvariantCulture),["dispatchToleranceSeconds"]=d.Schedule.DispatchToleranceSeconds.ToString(CultureInfo.InvariantCulture),
        ["description"]=d.Schedule.Description,["enabled"]=d.Enabled.ToString(),["installationStatus"]=d.InstallationStatus,["installationDetail"]=d.InstallationDetail,
        ["revision"]=d.Revision.ToString(CultureInfo.InvariantCulture)};
    public void Dispose() { model.DisposeAsync().AsTask().GetAwaiter().GetResult(); scope?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
}
