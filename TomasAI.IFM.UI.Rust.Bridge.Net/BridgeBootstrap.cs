using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
public unsafe struct ApiV4
{
    public ApiV1 Base;
    public delegate* unmanaged[Cdecl]<ulong,byte*,int,int> Submit;
}
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ApiV5
{
    public ApiV4 Base;
    public delegate* unmanaged[Cdecl]<ulong,uint,ulong*,byte*,int,int*,long*,long*,int> CopyChanged;
}
/// <summary>Versioned C ABI. Managed publications are copied into caller-owned uncompressed MessagePack buffers (ABI v4/v5).</summary>
public static unsafe class BridgeBootstrap
{
    static readonly ConcurrentDictionary<ulong,Session> Sessions=new();
    static long next;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static int GetApi(uint version,ApiV1* api,uint size)
    {
        if(api==null || version is not (4 or 5) || size<(version==5?(uint)sizeof(ApiV5):(uint)sizeof(ApiV4)))return -1;
        var table=new ApiV4 {Base=new(){Version=version,Size=version==5?(uint)sizeof(ApiV5):(uint)sizeof(ApiV4),Create=&Create,Copy=&Copy,Action=&Action,Destroy=&Destroy},Submit=&Submit};
        if(version==5)*((ApiV5*)api)=new(){Base=table,CopyChanged=&CopyChanged};else *((ApiV4*)api)=table;
        return 0;
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
        var bytes=session.Bytes(kind);if(bytes is null)return -1;
        *required=bytes.Length;if(bytes.Length==0)return 0;if(capacity<bytes.Length)return 1;if(buffer==null)return -1;
        bytes.CopyTo(new Span<byte>(buffer,bytes.Length));return 0;
    }
    /// <summary>0 full, 3 patch, 2 unchanged, 1 retry with required capacity. Cursor advances only after a successful copy.</summary>
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    static int CopyChanged(ulong handle,uint kind,ulong* cursor,byte* buffer,int capacity,int* required,long* producedTicks,long* sourceTicks)
    {
        if(cursor==null||required==null||producedTicks==null||sourceTicks==null||capacity<0||capacity>16*1024*1024||kind>8)return -1;
        if(!Sessions.TryGetValue(handle,out var session))return -3;
        try{
            var bytes=session.Bytes(kind)!;var update=session.Transfer(kind).Read(bytes,*cursor);
            if(update is null){*required=0;return 2;}
            var value=update.Value;*required=value.Bytes.Length;*producedTicks=value.ProducedTicks;*sourceTicks=value.SourceTicks;
            if(capacity<value.Bytes.Length)return 1;if(buffer==null&&value.Bytes.Length>0)return -1;
            value.Bytes.CopyTo(new Span<byte>(buffer,value.Bytes.Length));*cursor=value.Sequence;return value.Patch?3:0;
        }catch(Exception error){Console.Error.WriteLine(error);return -2;}
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
        try
        {
            var bytes=new ReadOnlySpan<byte>(buffer,length).ToArray();
            var header=BridgeWire.Deserialize<BridgeIntentHeader>(bytes);
            if(header.View=="trade-orders")return session.SubmitTrade(BridgeWire.Deserialize<ContractEditorIntent>(bytes));
            if(ContractEditorGateway.SupportsView(header.View))return session.Submit(BridgeWire.Deserialize<ContractEditorIntent>(bytes));
            if(header.View is "observation" or "risk")return session.Submit(BridgeWire.Deserialize<InspectionIntent>(bytes));
            return session.Submit(BridgeWire.Deserialize<EditorIntent>(bytes));
        }
        catch(MessagePack.MessagePackSerializationException){return -1;}
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
    readonly PublicationTransfer[] transfers=Enumerable.Range(0,9).Select(_=>new PublicationTransfer(incremental:Environment.GetEnvironmentVariable("IFM_UI_INCREMENTAL_PATCHES")=="1",trace:Environment.GetEnvironmentVariable("IFM_UI_LATENCY_OUTPUT") is not null)).ToArray();
    public PublicationTransfer Transfer(uint kind)=>transfers[kind];
    public byte[]? Bytes(uint kind)=>kind switch{0=>Publication,1=>Catalog,2=>Grid,3=>EditorPublication,4=>InspectionPublication,5=>ContractPublication,6=>DashboardPublication,7=>SidebarPublication,8=>TradePublication,_=>null};
    readonly BlockingCollection<uint> actions=new(32);
    readonly Thread owner;
    readonly DashboardSession dashboard = new();
    public byte[] DashboardPublication => dashboard.Publication;
    public byte[] SidebarPublication => dashboard.SidebarPublication;
    readonly ContractEditorGateway trades;
    TradeWorkspaceSession? tradeWorkspace;
    readonly object tradePublicationGate=new();
    ContractEditorPublication? lastTrade;
    Dictionary<string,string>? lastSubmission;
    byte[]? lastConfirmation;
    byte[] tradeBytes=[];
    public byte[] TradePublication {get{
        lock(editorGate) {
            if(tradeWorkspace is {RefreshPending:true} backend)
                trades.TryRefresh("trade-orders",backend.RefreshPublicationAsync);
        }
        var snapshot=trades.Snapshot();var confirmation=tradeWorkspace?.Confirmation;
        var submission=tradeWorkspace?.SubmissionContext;
        lock(tradePublicationGate) {
            if(ReferenceEquals(lastTrade,snapshot)&&ReferenceEquals(lastConfirmation,confirmation)&&ReferenceEquals(lastSubmission,submission))return tradeBytes;
            var merged=snapshot;
            if(submission is not null){var context=new Dictionary<string,string>(snapshot.Result.Context??[]);foreach(var pair in submission)context[pair.Key]=pair.Value;merged=merged with{Result=merged.Result with{Context=context}};}
            if(confirmation is not null){var documents=new Dictionary<string,byte[]>(merged.Result.Documents){["confirmation"]=confirmation};merged=merged with{Result=merged.Result with{Documents=documents}};}
            var bytes=BridgeWire.Serialize(merged);
            lastTrade=snapshot;lastSubmission=submission;lastConfirmation=confirmation;
            return tradeBytes=bytes;
        }
    }}
    public int SubmitTrade(ContractEditorIntent intent) => intent.Intent is "confirm-order" or "cancel-order-confirmation" ? tradeWorkspace?.Decide(intent)??-5 : trades.Submit(intent);
    readonly EditorActionGateway editors;
    readonly InspectionActionGateway inspections;
    readonly ContractEditorGateway contracts;
    readonly Dictionary<string,ContractEditorSession> contractSessions=[];
    PortfolioSession? portfolioEditor;
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
        get {lock(editorGate){if(scheduledEditor is {RefreshPending:true} scheduled)contracts.TryRefresh("scheduled-tasks",scheduled.RefreshPublicationAsync);if(healthEditor is {RefreshDue:true} health)contracts.TryRefresh("operations-health",health.RefreshAsync);if(actorHealthEditor is {RefreshDue:true} actorHealth)contracts.TryRefresh("actor-health",actorHealth.RefreshAsync);}return contracts.WireSnapshot();}
    }
    public int Submit(ContractEditorIntent intent)=>contracts.Submit(intent);
    StrategyInspectionBackend? inspectionBackend;
    public byte[] InspectionPublication=>inspections.WireSnapshot();
    public int Submit(InspectionIntent intent)=>inspections.Submit(intent);
    readonly object editorGate=new();
    LookupEditorSession? lookupEditor;
    public byte[] EditorPublication=>editors.WireSnapshot();
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
        contracts=new(async intent=>{if(Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")!="live")throw new InvalidOperationException("Contract editing requires live services");if(intent.View=="portfolio"){PortfolioSession portfolio;lock(editorGate)portfolio=portfolioEditor??=new();return await portfolio.ExecuteAsync(intent);}if(intent.View=="strategy-catalog"){StrategyCatalogSession catalog;lock(editorGate)catalog=catalogEditor??=new();return await catalog.ExecuteAsync(intent);}if(intent.View=="parameter-sets"){ParameterSetSession parameters;lock(editorGate)parameters=parameterEditor??=new();return await parameters.ExecuteAsync(intent);}if(intent.View=="actor-health"){ActorHealthSession actorHealth;lock(editorGate)actorHealth=actorHealthEditor??=new();return await actorHealth.ExecuteAsync(intent);}if(intent.View=="operations-health"){OperationsHealthSession health;lock(editorGate)health=healthEditor??=new();return await health.ExecuteAsync(intent);}if(intent.View=="scheduled-tasks"){ScheduledTaskSession scheduled;lock(editorGate)scheduled=scheduledEditor??=new();return await scheduled.ExecuteAsync(intent);}if(intent.View=="provider-definitions"){ProviderDefinitionSession provider;lock(editorGate)provider=providerEditor??=new();return await provider.ExecuteAsync(intent);}if(intent.View=="economic-calendar"){EconomicCalendarEditorSession calendar;lock(editorGate)calendar=calendarEditor??=new();return await calendar.ExecuteAsync(intent);}if(intent.View=="yield-curve"){YieldCurveEditorSession yield;lock(editorGate)yield=yieldEditor??=new();return await yield.ExecuteAsync(intent);}ContractEditorSession backend;lock(editorGate){if(!contractSessions.TryGetValue(intent.View,out backend!))contractSessions.Add(intent.View,backend=new(intent.View=="option-contract"));}return await backend.ExecuteAsync(intent);});
        inspections=new(async intent=>{if(Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")!="live")throw new InvalidOperationException("Inspection requires live services");StrategyInspectionBackend backend;lock(editorGate)backend=inspectionBackend??=new();return await backend.ExecuteAsync(intent);});
        editors=new(async intent=>{if(Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")!="live")throw new InvalidOperationException("Editing requires live Development services");LookupEditorSession editor;lock(editorGate)editor=lookupEditor??=new();return await editor.ExecuteAsync(intent);});
        Catalog=BridgeWire.Serialize(BridgeCatalogData.Create());
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
            while(!actions.IsCompleted){if(actions.TryTake(out var action,40)){if(dashboard.SelectSidebar(action))continue;if(action==3){Volatile.Write(ref grid,BridgeWire.Serialize(VirtualGridDataset.Create()));continue;}if(live is not null)live.Select(action);else if(action<2)selected=(int)action;revision++;Publish();}else if(live is not null&&(observed!=Volatile.Read(ref live.ChangeVersion)||lastStatus!=live.Status)){revision++;Publish();}}
            void Publish(){var snapshot=live is null?adapter.CreateFixture(selected,revision):live.Snapshot(adapter,revision,epoch);if(live is not null){observed=Volatile.Read(ref live.ChangeVersion);lastStatus=live.Status;}Volatile.Write(ref publication,BridgeWire.Serialize(snapshot));}

        }
        catch(Exception e){Console.Error.WriteLine(e);Volatile.Write(ref publication,BridgeWire.Serialize(new{error=e.ToString()}));}
    }
    public bool Enqueue(uint action){try{return actions.TryAdd(action);}catch(InvalidOperationException){return false;}}
    public void Dispose()
    {
        tradeWorkspace?.CancelPending();dashboard.Dispose();actions.CompleteAdding();
        if(!owner.Join(TimeSpan.FromSeconds(5)))throw new TimeoutException("View owner shutdown timed out");
        // Stop accepting work and drain all owners before disposing their models/subscriptions.
        var drained=Task.WhenAll(trades.DisposeAsync().AsTask(),contracts.DisposeAsync().AsTask(),
            editors.DisposeAsync().AsTask(),inspections.DisposeAsync().AsTask());
        if(!drained.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("UI session owner shutdown timed out");
        lock(editorGate){
            tradeWorkspace?.Dispose();lookupEditor?.Dispose();inspectionBackend?.Dispose();portfolioEditor?.Dispose();yieldEditor?.Dispose();
            calendarEditor?.Dispose();providerEditor?.Dispose();scheduledEditor?.Dispose();healthEditor?.Dispose();
            actorHealthEditor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            parameterEditor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            catalogEditor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            foreach(var backend in contractSessions.Values)backend.Dispose();
        }
        foreach(var transfer in transfers)transfer.Dispose();
        actions.Dispose();container.Dispose();
    }
}
