using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using SimpleInjector;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ApiV1
{
    public uint Version, Size;
    public delegate* unmanaged[Cdecl]<ulong*,int> Create;
    public delegate* unmanaged[Cdecl]<ulong,uint,byte*,int,int*,int> Copy;
    public delegate* unmanaged[Cdecl]<ulong,uint,int> Action;
    public delegate* unmanaged[Cdecl]<ulong,int> Destroy;
}
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ApiV2
{
    public ApiV1 Base;
    public delegate* unmanaged[Cdecl]<ulong,byte*,int,int> Submit;
}
/// <summary>Versioned C ABI. Managed publications are copied into caller-owned UTF-8 buffers.</summary>
public static unsafe class BridgeBootstrap
{
    static readonly ConcurrentDictionary<ulong,Session> Sessions=new();
    static long next;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static int GetApi(uint version,ApiV1* api,uint size)
    {
        if(api==null)return -1;
        if(version==2){if(size<(uint)sizeof(ApiV2))return -1;*((ApiV2*)api)=new(){Base=new(){Version=2,Size=(uint)sizeof(ApiV2),Create=&Create,Copy=&Copy,Action=&Action,Destroy=&Destroy},Submit=&Submit};return 0;}
        if(version!=1||size<(uint)sizeof(ApiV1)) return -1;
        *api=new(){Version=1,Size=(uint)sizeof(ApiV1),Create=&Create,Copy=&Copy,Action=&Action,Destroy=&Destroy};return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    static int Create(ulong* handle)
    {
        if(handle==null)return -1;
        try{var id=(ulong)Interlocked.Increment(ref next);Sessions[id]=new();*handle=id;return 0;}catch(Exception e){Console.Error.WriteLine(e);return -2;}
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    static int Copy(ulong handle,uint kind,byte* buffer,int capacity,int* required)
    {
        if(required==null||capacity<0||capacity>16*1024*1024)return -1;
        if(!Sessions.TryGetValue(handle,out var session))return -3;
        var bytes=kind switch{0=>session.Publication,1=>session.Catalog,2=>session.Grid,3=>session.EditorPublication,4=>session.InspectionPublication,5=>session.ContractPublication,6=>session.DashboardPublication,7=>session.SidebarPublication,8=>session.TradePublication,_=>null};if(bytes is null)return -1;
        *required=bytes.Length;if(bytes.Length==0)return 0;if(capacity<bytes.Length)return 1;if(buffer==null)return -1;
        bytes.CopyTo(new Span<byte>(buffer,bytes.Length));return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    static int Action(ulong handle,uint action)
    {
        if(!Sessions.TryGetValue(handle,out var session))return -3;
        if(action>3&&action is not (>=10 and <=13)&&action<1000)return -1;
        return session.Enqueue(action)?0:-4;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    static int Submit(ulong handle,byte* buffer,int length)
    {
        if(!Sessions.TryGetValue(handle,out var session))return -3;
        if(buffer==null||length<=0||length>262144)return -1;
        try{using var document=JsonDocument.Parse(new ReadOnlySpan<byte>(buffer,length).ToArray());if(document.RootElement.TryGetProperty("view",out var tradeView)&&tradeView.GetString()=="trade-orders"){var trade=JsonSerializer.Deserialize<ContractEditorIntent>(new ReadOnlySpan<byte>(buffer,length),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});return trade is null?-1:session.SubmitTrade(trade);}if(document.RootElement.TryGetProperty("view",out var contractView)&&ContractEditorGateway.SupportsView(contractView.GetString())){var contract=JsonSerializer.Deserialize<ContractEditorIntent>(new ReadOnlySpan<byte>(buffer,length),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});return contract is null?-1:session.Submit(contract);}if(document.RootElement.TryGetProperty("view",out var view)&&view.GetString() is "observation" or "risk"){var inspection=JsonSerializer.Deserialize<InspectionIntent>(new ReadOnlySpan<byte>(buffer,length),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});return inspection is null?-1:session.Submit(inspection);}var intent=JsonSerializer.Deserialize<EditorIntent>(new ReadOnlySpan<byte>(buffer,length),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});return intent is null?-1:session.Submit(intent);}
        catch(JsonException){return -1;}
        catch(Exception e){Console.Error.WriteLine(e);return -2;}
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    static int Destroy(ulong handle)
    {
        if(!Sessions.TryRemove(handle,out var session))return -3;
        try{session.Dispose();return 0;}catch(Exception e){Console.Error.WriteLine(e);return -2;}
    }
}
sealed class Session : IDisposable
{
    static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    readonly BlockingCollection<uint> actions=new(32);
    readonly Thread owner;
    readonly DashboardSession dashboard = new();
    public byte[] DashboardPublication => dashboard.Publication;
    public byte[] SidebarPublication => dashboard.SidebarPublication;
    readonly ContractEditorGateway trades;
    TradeWorkspaceSession? tradeWorkspace;
    public byte[] TradePublication {get{
        var snapshot=trades.Snapshot();var confirmation=tradeWorkspace?.Confirmation;
        if(confirmation is not null){var context=new Dictionary<string,string>(snapshot.Result.Context??[]){["confirmation"]=confirmation};snapshot=snapshot with{Result=snapshot.Result with{Context=context}};}
        return JsonSerializer.SerializeToUtf8Bytes(snapshot,JsonOptions);
    }}
    public int SubmitTrade(ContractEditorIntent intent) => intent.Intent is "confirm-order" or "cancel-order-confirmation" ? tradeWorkspace?.Decide(intent)??-5 : trades.Submit(intent);
    readonly EditorActionGateway editors;
    readonly InspectionActionGateway inspections;
    readonly ContractEditorGateway contracts;
    readonly Dictionary<string,ContractEditorSession> contractSessions=[];
    YieldCurveEditorSession? yieldEditor;
    EconomicCalendarEditorSession? calendarEditor;
    ProviderDefinitionSession? providerEditor;
    ScheduledTaskSession? scheduledEditor;
    OperationsHealthSession? healthEditor;
    ActorHealthSession? actorHealthEditor;
    ParameterSetSession? parameterEditor;
    StrategyCatalogSession? catalogEditor;
    public byte[] ContractPublication
    {
        get {lock(editorGate){if(scheduledEditor is {RefreshPending:true} scheduled)contracts.TryRefresh("scheduled-tasks",scheduled.RefreshPublicationAsync);if(healthEditor is {RefreshDue:true} health)contracts.TryRefresh("operations-health",health.RefreshAsync);if(actorHealthEditor is {RefreshDue:true} actorHealth)contracts.TryRefresh("actor-health",actorHealth.RefreshAsync);}return JsonSerializer.SerializeToUtf8Bytes(contracts.Snapshot(),JsonOptions);}
    }
    public int Submit(ContractEditorIntent intent)=>contracts.Submit(intent);
    StrategyInspectionBackend? inspectionBackend;
    public byte[] InspectionPublication=>JsonSerializer.SerializeToUtf8Bytes(inspections.Snapshot(),JsonOptions);
    public int Submit(InspectionIntent intent)=>inspections.Submit(intent);
    readonly object editorGate=new();
    LookupEditorSession? lookupEditor;
    public byte[] EditorPublication=>JsonSerializer.SerializeToUtf8Bytes(editors.Snapshot(),JsonOptions);
    public int Submit(EditorIntent intent)=>editors.Submit(intent);
    readonly Container container=new();
    byte[] publication=[];
    public byte[] Publication=>Volatile.Read(ref publication);
    public byte[] Catalog{get;}
    byte[] grid=[]; public byte[] Grid=>Volatile.Read(ref grid);
    public Session()
    {
        trades = new(async intent => {
            if(Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")!="live")throw new InvalidOperationException("Trade Orders requires live services");
            TradeWorkspaceSession backend;lock(editorGate)backend=tradeWorkspace??=new();
            return await backend.ExecuteAsync(intent);
        }, TradeWorkspaceSession.Validate);
        contracts=new(async intent=>{if(Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")!="live")throw new InvalidOperationException("Contract editing requires live services");if(intent.View=="strategy-catalog"){StrategyCatalogSession catalog;lock(editorGate)catalog=catalogEditor??=new();return await catalog.ExecuteAsync(intent);}if(intent.View=="parameter-sets"){ParameterSetSession parameters;lock(editorGate)parameters=parameterEditor??=new();return await parameters.ExecuteAsync(intent);}if(intent.View=="actor-health"){ActorHealthSession actorHealth;lock(editorGate)actorHealth=actorHealthEditor??=new();return await actorHealth.ExecuteAsync(intent);}if(intent.View=="operations-health"){OperationsHealthSession health;lock(editorGate)health=healthEditor??=new();return await health.ExecuteAsync(intent);}if(intent.View=="scheduled-tasks"){ScheduledTaskSession scheduled;lock(editorGate)scheduled=scheduledEditor??=new();return await scheduled.ExecuteAsync(intent);}if(intent.View=="provider-definitions"){ProviderDefinitionSession provider;lock(editorGate)provider=providerEditor??=new();return await provider.ExecuteAsync(intent);}if(intent.View=="economic-calendar"){EconomicCalendarEditorSession calendar;lock(editorGate)calendar=calendarEditor??=new();return await calendar.ExecuteAsync(intent);}if(intent.View=="yield-curve"){YieldCurveEditorSession yield;lock(editorGate)yield=yieldEditor??=new();return await yield.ExecuteAsync(intent);}ContractEditorSession backend;lock(editorGate){if(!contractSessions.TryGetValue(intent.View,out backend!))contractSessions.Add(intent.View,backend=new(intent.View=="option-contract"));}return await backend.ExecuteAsync(intent);});
        inspections=new(async intent=>{if(Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")!="live")throw new InvalidOperationException("Inspection requires live services");StrategyInspectionBackend backend;lock(editorGate)backend=inspectionBackend??=new();return await backend.ExecuteAsync(intent);});
        editors=new(async intent=>{if(Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")!="live")throw new InvalidOperationException("Editing requires live Development services");LookupEditorSession editor;lock(editorGate)editor=lookupEditor??=new();return await editor.ExecuteAsync(intent);});
        using var stream=typeof(Session).Assembly.GetManifestResourceStream("Strings.json")!;
        using var memory=new MemoryStream();stream.CopyTo(memory);Catalog=memory.ToArray();
        owner=new Thread(Run){IsBackground=true,Name="IFM Rust managed view owner"};owner.Start();
    }
    void Run()
    {
        try
        {
            container.RegisterSingleton<StrategyDetailsAdapter>();container.Verify();
            var adapter=container.GetInstance<StrategyDetailsAdapter>();long revision=1;int selected=0;
            using var live=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")=="live"?new LiveStrategySession():null;
            var epoch=Guid.NewGuid().ToString("D"); long observed=-1; string? lastStatus=null;
            Publish();
            while(!actions.IsCompleted){if(actions.TryTake(out var action,100)){if(dashboard.SelectSidebar(action))continue;if(action==3){Volatile.Write(ref grid,JsonSerializer.SerializeToUtf8Bytes(VirtualGridDataset.Create(),JsonOptions));continue;}if(live is not null)live.Select(action);else if(action<2)selected=(int)action;revision++;Publish();}else if(live is not null&&(observed!=Volatile.Read(ref live.ChangeVersion)||lastStatus!=live.Status)){revision++;Publish();}}
            void Publish(){var snapshot=live is null?adapter.CreateFixture(selected,revision):live.Snapshot(adapter,revision,epoch);if(live is not null){observed=Volatile.Read(ref live.ChangeVersion);lastStatus=live.Status;}Volatile.Write(ref publication,JsonSerializer.SerializeToUtf8Bytes(snapshot,JsonOptions));}

        }
        catch(Exception e){Console.Error.WriteLine(e);Volatile.Write(ref publication,JsonSerializer.SerializeToUtf8Bytes(new{error=e.ToString()}));}
    }
    public bool Enqueue(uint action){try{return actions.TryAdd(action);}catch(InvalidOperationException){return false;}}
    public void Dispose(){tradeWorkspace?.CancelPending();dashboard.Dispose();actions.CompleteAdding();if(!owner.Join(TimeSpan.FromSeconds(5)))throw new TimeoutException("View owner shutdown timed out");if(!contracts.DrainAsync().Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Contract editor shutdown timed out");if(!trades.DrainAsync().Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Trade workspace shutdown timed out");lock(editorGate){tradeWorkspace?.Dispose();lookupEditor?.Dispose();inspectionBackend?.Dispose();yieldEditor?.Dispose();calendarEditor?.Dispose();providerEditor?.Dispose();scheduledEditor?.Dispose();healthEditor?.Dispose();actorHealthEditor?.DisposeAsync().AsTask().GetAwaiter().GetResult();parameterEditor?.DisposeAsync().AsTask().GetAwaiter().GetResult();catalogEditor?.DisposeAsync().AsTask().GetAwaiter().GetResult();foreach(var backend in contractSessions.Values)backend.Dispose();}if(!inspections.DrainAsync().Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Inspection shutdown timed out");if(!editors.DrainAsync().Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Editor command shutdown timed out");if(!contracts.DrainAsync().Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Contract editor shutdown timed out");actions.Dispose();container.Dispose();}
}

