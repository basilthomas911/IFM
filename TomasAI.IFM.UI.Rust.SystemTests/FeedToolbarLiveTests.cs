using System.Diagnostics;
using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class FeedToolbarLiveTests
{
    [Fact]
    public void Running_Rust_window_exposes_enabled_Stop_Market_Feeds_button()
    {
        using var process=Process.GetProcessById(int.Parse(Environment.GetEnvironmentVariable("IFM_RUST_TEST_UI_PID")!));
        using var automation=new FlaUI.UIA3.UIA3Automation();
        var clock=Stopwatch.StartNew();
        while(clock.Elapsed<TimeSpan.FromSeconds(90)){
            process.Refresh();Assert.False(process.HasExited);
            if(process.MainWindowHandle!=IntPtr.Zero){
                var window=automation.FromHandle(process.MainWindowHandle);
                var button=window.FindFirstDescendant(cf=>cf.ByName("Stop Market Feeds"));
                if(button?.IsEnabled==true)return;
            }
            Thread.Sleep(500);
        }
        throw new TimeoutException("The running Rust UI did not expose an enabled Stop Market Feeds button.");
    }

    [Fact]
    public unsafe void Rust_toolbar_stops_and_restarts_feeds_through_the_WinForms_view_model()
    {
        var previous=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");
        Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
        delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;
        ApiV4 table; Assert.Equal(0,get(4,(ApiV1*)&table,(uint)sizeof(ApiV4)));var api=table.Base;
        ulong handle;Assert.Equal(0,api.Create(&handle));
        var evidence=new List<object>();
        using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(5)};
        string Boot() {using var health=JsonDocument.Parse(http.GetStringAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult());return health.RootElement.GetProperty("entries").GetProperty("application_lifecycle").GetProperty("data").GetProperty("processBootId").GetString()!;}
        var boot=Boot();
        try {
            var ready=Wait(api,handle,x=>x.FeedControl is {Ready:true,CanToggle:true,Busy:false},TimeSpan.FromSeconds(120));
            Assert.True(ready.FeedControl.Active,"Live test expects initially running feeds so that they can be restored afterwards.");
            evidence.Add(new {phase="Before",control=ready.FeedControl,apiBootId=boot});
            Assert.Equal(0,api.Action(handle,SidebarSession.FeedToggleAction));
            // Repeated queued clicks must not invert the operator's requested stop.
            Assert.Equal(0,api.Action(handle,SidebarSession.FeedToggleAction));
            var stopped=Wait(api,handle,x=>x.FeedControl is {Ready:true,CanToggle:true,Busy:false,Active:false},TimeSpan.FromSeconds(120));
            evidence.Add(new {phase="Stopped",control=stopped.FeedControl,logs=stopped.StatusRows.Take(5)});
            Assert.Equal(boot,Boot());
            Assert.Equal(0,api.Action(handle,SidebarSession.FeedToggleAction));
            var started=Wait(api,handle,x=>x.FeedControl is {Ready:true,CanToggle:true,Busy:false,Active:true},TimeSpan.FromSeconds(120));
            Assert.Equal(boot,Boot());
            evidence.Add(new {phase="Started",control=started.FeedControl,logs=started.StatusRows.Take(5)});
        } finally {
            try {
                var state=Read(api,handle);
                if(state.FeedControl is {Ready:true,CanToggle:true,Busy:false,Active:false}) {
                    api.Action(handle,SidebarSession.FeedToggleAction);
                    Wait(api,handle,x=>x.FeedControl is {Busy:false,Active:true},TimeSpan.FromSeconds(120));
                }
            } finally {api.Destroy(handle);Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",previous);}
            var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??Path.Combine(AppContext.BaseDirectory,"feed-toolbar-evidence");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"feed-toolbar-live.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions {WriteIndented=true}));
        }
    }
    static unsafe SidebarPublication Wait(ApiV1 api,ulong handle,Func<SidebarPublication,bool> predicate,TimeSpan timeout)
    {
        var timer=Stopwatch.StartNew();SidebarPublication snapshot;
        do {snapshot=Read(api,handle);if(predicate(snapshot))return snapshot;Thread.Sleep(150);}while(timer.Elapsed<timeout);
        throw new TimeoutException("Feed toolbar did not reach expected state: "+JsonSerializer.Serialize(snapshot));
    }
    static unsafe SidebarPublication Read(ApiV1 api,ulong handle)
    {
        for(var attempt=0;attempt<10;attempt++){
            int length;api.Copy(handle,7,null,0,&length);var buffer=new byte[length];
            fixed(byte* bytes=buffer){var status=api.Copy(handle,7,bytes,length,&length);if(status==1)continue;Assert.Equal(0,status);}
            return BridgeWire.Deserialize<SidebarPublication>(buffer.AsMemory(0,length))!;
        }
        throw new InvalidOperationException("Sidebar publication kept growing.");
    }
}
