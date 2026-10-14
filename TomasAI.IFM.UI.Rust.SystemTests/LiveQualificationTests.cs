using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using Xunit;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using NativeApp = FlaUI.Core.Application;
public unsafe class LiveQualificationTests
{
 [Fact] public void Live_backend_supplies_workflow_details()
 {
  AssertBackendReady();
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV1 api;get(1,&api,40);ulong handle;Assert.Equal(0,api.Create(&handle));
  string status="No publication";
  try {
   var deadline=DateTime.UtcNow.AddSeconds(30);
   do {
    int size;var code=api.Copy(handle,0,null,0,&size);if(code<0)throw new Exception("Copy failed "+code);
    if(size>0){var bytes=new byte[size];fixed(byte* p=bytes)code=api.Copy(handle,0,p,size,&size);if(code==0){using var json=JsonDocument.Parse(bytes);var data=json.RootElement;
      var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"live-qualification.json"),bytes);
      if(data.TryGetProperty("error",out var error))throw new Exception(error.GetString());
      status=data.GetProperty("status").GetString()!;
      if(data.GetProperty("workflows").GetArrayLength()>0&&data.GetProperty("roots").GetArrayLength()>0){
       var generation=data.GetProperty("generation").GetInt64();
       var selected=data.GetProperty("workflowId").GetString();
       Assert.Equal(0,api.Action(handle,data.GetProperty("workflows")[0].GetProperty("action").GetUInt32()));
       Assert.Equal(0,api.Action(handle,2));
       var reloadDeadline=DateTime.UtcNow.AddSeconds(30);
       do {Thread.Sleep(100);int n;api.Copy(handle,0,null,0,&n);var reloaded=new byte[n];fixed(byte* q=reloaded){if(api.Copy(handle,0,q,n,&n)!=0)continue;}
        using var reload=JsonDocument.Parse(reloaded);var r=reload.RootElement;
        if(r.GetProperty("generation").GetInt64()>generation&&r.GetProperty("roots").GetArrayLength()>0&&!r.GetProperty("status").GetString()!.Contains("Connecting")){
         Assert.Equal(selected,r.GetProperty("workflowId").GetString());File.WriteAllBytes(Path.Combine(dir,"live-reloaded.json"),reloaded);return;
        }
       }while(DateTime.UtcNow<reloadDeadline);
       Assert.Fail("Live reconnect/reload did not republish the selected persisted workflow");
      }
      if(status.Contains("Unavailable")||status.Contains("Disconnected"))break;
    }}
    Thread.Sleep(100);
   }while(DateTime.UtcNow<deadline);
   Assert.Fail("Live workflow backend did not supply details: "+status);
  }finally {var result=api.Destroy(handle);Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);Assert.Equal(0,result);}
 }
 [Fact] public void Live_native_lookup_editor_loads_selects_and_cancels_removal()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try
  {
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);
   do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);
   Assert.NotNull(window);WaitLiveStatus(window);
   window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&e.Properties.Name.ValueOrDefault=="Lookup Definitions").AsButton().Invoke();
   Wait(window,"Load lookups").AsButton().Invoke();Wait(window,"Completed: Backend completion received");
   var row=window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&(e.Properties.Name.ValueOrDefault??"").Contains(" | "));row.AsButton().Invoke();
   Wait(window,"Save lookup");Wait(window,"Remove lookup").AsButton().Invoke();Wait(window,"Remove the selected lookup definition?");Wait(window,"Cancel").AsButton().Invoke();
   Assert.DoesNotContain(window.FindAllDescendants(),e=>e.Properties.Name.ValueOrDefault=="Confirm removal");
   Assert.Contains(window.FindAllDescendants(),e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Edit&&e.Properties.Name.ValueOrDefault=="Short code");
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);window.Capture().Save(Path.Combine(dir,"live-native-lookup.png"));
  }
  finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Live_native_observation_and_risk_history_render_backend_results()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);var workflow=LiveInspectionTests.ReadWorkflowIdentity();
  var pair=Task.Run(LiveFundSelection.SelectAsync).GetAwaiter().GetResult();
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try{
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);Assert.NotNull(window);WaitLiveStatus(window);
   FlaUI.Core.AutomationElements.Button Nav(string name)=>window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&e.Properties.Name.ValueOrDefault==name).AsButton();
   FlaUI.Core.AutomationElements.TextBox Field(string name)=>window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Edit&&e.Properties.Name.ValueOrDefault==name).AsTextBox();
   void Enter(string name,string value){var field=Field(name);field.Focus();FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);FlaUI.Core.Input.Keyboard.Type(value);}
   Nav("Strategy Observation").Invoke();Enter("Workflow ID",Guid.Parse(workflow).ToString("D"));Nav("Open workflow").Invoke();Wait(window,"Loaded");Wait(window,"Workflow Pipeline");Nav("Workflow").Invoke();Wait(window,"Inspect Workflow").AsButton().Invoke();Wait(window,"WorkflowRevision");window.Capture().Save(Path.Combine(dir,"live-native-observation.png"));
   Nav("Risk History").Invoke();Enter("Portfolio ID",pair.PortfolioId.ToString());Enter("Fund ID",pair.FundId.ToString());Nav("Load history").Invoke();Wait(window,"No history for these filters");Nav("Evidence").Invoke();Wait(window,"Decision and sizing");window.Capture().Save(Path.Combine(dir,"live-native-risk-history.png"));
  }catch{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero){var failed=automation.FromHandle(h).AsWindow();failed.Capture().Save(Path.Combine(dir,"inspection-failure.png"));File.WriteAllText(Path.Combine(dir,"inspection-failure-uia.txt"),string.Join(Environment.NewLine,failed.FindAllDescendants().Select(e=>$"{e.ControlType}: {e.Properties.Name.ValueOrDefault}")));}throw;}finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Live_native_yield_editor_selects_all_rates_and_cancels_removal()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try{
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);Assert.NotNull(window);WaitLiveStatus(window);
   void Button(string name)=>window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&e.Properties.Name.ValueOrDefault==name).AsButton().Invoke();
   Button("Yield Curve Rates");Button("Load rates");
   FlaUI.Core.AutomationElements.AutomationElement? row=null;deadline=DateTime.UtcNow.AddSeconds(30);
   do{try{row=window.FindAllDescendants().FirstOrDefault(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&DateOnly.TryParseExact(e.Properties.Name.ValueOrDefault,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _));}catch(System.Runtime.InteropServices.COMException error) when(error.HResult==unchecked((int)0x80040201)){row=null;}if(row is null)Thread.Sleep(100);}while(row is null&&DateTime.UtcNow<deadline);
   Assert.NotNull(row);row.AsButton().Invoke();Wait(window,"Save rate");
   foreach(var tenor in new[]{"1M","2M","3M","6M","1Y","2Y","3Y","5Y","7Y","10Y","20Y","30Y"})Assert.Contains(window.FindAllDescendants(),e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Edit&&e.Properties.Name.ValueOrDefault==tenor);
   Wait(window,"Import rates");Button("Remove rate");Wait(window,"Remove the selected yield curve?");Button("Cancel");Assert.DoesNotContain(window.FindAllDescendants(),e=>e.Properties.Name.ValueOrDefault=="Confirm removal");
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);window.Capture().Save(Path.Combine(dir,"live-native-yield-curve.png"));
  }finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Live_native_contract_editors_load_select_and_cancel_without_mutating()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try{
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);Assert.NotNull(window);WaitLiveStatus(window);
   void Button(string label)=>window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&e.Properties.Name.ValueOrDefault==label).AsButton().Invoke();
   foreach(var view in new[]{"Futures Contracts","Option Contracts"}){
    Button(view);Button("Load contracts");Wait(window,"Completed: Backend completion received");
    var row=window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&(e.Properties.Name.ValueOrDefault??"").StartsWith("ES"));row.AsButton().Invoke();
    Button("Remove contract");Wait(window,"Remove the selected contract?");Button("Cancel");Assert.DoesNotContain(window.FindAllDescendants(),e=>e.Properties.Name.ValueOrDefault=="Confirm removal");
    var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);window.Capture().Save(Path.Combine(dir,view=="Futures Contracts"?"live-native-futures-contracts.png":"live-native-option-contracts.png"));
   }
  }finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Live_native_calendar_loads_fields_filters_and_cancels_removal()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try{
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);Assert.NotNull(window);WaitLiveStatus(window);
   void Button(string name)=>window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&e.Properties.Name.ValueOrDefault==name).AsButton().Invoke();
   Button("Economic Calendar");Button("Load calendar");Wait(window,"Completed: Backend completion received");
   var filter=window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Edit&&e.Properties.Name.ValueOrDefault=="Filter date (Eastern)").AsTextBox();filter.Focus();FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);FlaUI.Core.Input.Keyboard.Type("2026-10-20");Button("Apply filter");
   var row=Wait(window,"2026-10-20T12:30:00.0000000Z");row.AsButton().Invoke();Wait(window,"Save event");
   foreach(var label in new[]{"Event time (UTC)","Event name","Actual","Forecast","Prior","Impact","Unit","Change","Change (%)","Created on","Created by"})Assert.Contains(window.FindAllDescendants(),e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Edit&&e.Properties.Name.ValueOrDefault==label);
   Button("Remove event");Wait(window,"Remove the selected calendar event?");Button("Cancel");Assert.DoesNotContain(window.FindAllDescendants(),e=>e.Properties.Name.ValueOrDefault=="Confirm removal");
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);window.Capture().Save(Path.Combine(dir,"live-native-calendar.png"));
  }finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Live_native_provider_browser_searches_cached_facts_without_feed_or_writes()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try{
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);Assert.NotNull(window);WaitLiveStatus(window);
   Wait(window,"Provider Definitions").AsButton().Invoke();Wait(window,"Search definitions").AsButton().Invoke();Wait(window,"ESZ6").AsButton().Invoke();Wait(window,"definitionDigest");Wait(window,"rawDefinitionReference");
   Assert.DoesNotContain(window.FindAllDescendants(),e=>e.Properties.Name.ValueOrDefault=="Confirm removal");
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);window.Capture().Save(Path.Combine(dir,"live-native-provider-browser.png"));
  }finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Live_native_provider_mapping_previews_existing_identity_and_cancels_without_writing()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try{
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);Assert.NotNull(window);WaitLiveStatus(window);
   Wait(window,"Provider Definitions").AsButton().Invoke();Wait(window,"Search definitions").AsButton().Invoke();Wait(window,"ESZ6").AsButton().Invoke();Wait(window,"Map selected definition").AsButton().Invoke();
   var target=window.FindFirstDescendant(cf=>cf.ByName("Existing contract ID").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))).AsTextBox();target.Focus();FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);FlaUI.Core.Input.Keyboard.Type("ES20261218");
   deadline=DateTime.UtcNow.AddSeconds(5);while(target.Text!="ES20261218" && DateTime.UtcNow<deadline)Thread.Sleep(50);Assert.Equal("ES20261218",target.Text);
   Wait(window,"Preview mapping").AsButton().Invoke();Wait(window,"Confirm draft import");Wait(window,"Change");Wait(window,"ES20261218");
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);window.Capture().Save(Path.Combine(dir,"live-native-provider-mapping.png"));
   Wait(window,"Cancel").AsButton().Invoke();Assert.DoesNotContain(window.FindAllDescendants(),e=>e.Properties.Name.ValueOrDefault=="Confirm draft import");
  }finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Live_native_provider_contract_conventions_can_be_inspected_and_cancelled()
 {
  AssertBackendReady();var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try{
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);do{var h=NativeWindows.Find(app.ProcessId);if(h!=IntPtr.Zero)window=automation.FromHandle(h).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);Assert.NotNull(window);WaitLiveStatus(window);
   Wait(window,"Option Contracts").AsButton().Invoke();Wait(window,"Load contracts").AsButton().Invoke();Wait(window,"Completed: Backend completion received");
   var row=window.FindAllDescendants().First(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&(e.Properties.Name.ValueOrDefault??"").StartsWith("ES"));row.AsButton().Invoke();Wait(window,"Review conventions").AsButton().Invoke();Wait(window,"Save conventions");Wait(window,"Review evidence");Wait(window,"Exchange time zone");
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);window.Capture().Save(Path.Combine(dir,"live-native-reference-review.png"));
   Wait(window,"Cancel").AsButton().Invoke();Assert.DoesNotContain(window.FindAllDescendants(),e=>e.Properties.Name.ValueOrDefault=="Confirm convention changes");Wait(window,"Save contract");
  }finally{app.Close();if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static void AssertBackendReady()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
  var uri=Environment.GetEnvironmentVariable("IFM_RUST_API_READINESS_URL")??"http://localhost:22543/health/launch-ready";
  using var response=http.GetAsync(uri).GetAwaiter().GetResult();
  Assert.True(response.IsSuccessStatusCode,"Start Development API and wait for launch readiness before live qualification: "+uri+" returned "+response.StatusCode);
 }
 [Fact] public void Live_native_window_renders_history_and_reloads()
 {
  AssertBackendReady();
  var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");
  Assert.True(File.Exists(exe));
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();
  try {
   Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);
   do {var handle=NativeWindows.Find(app.ProcessId);if(handle!=IntPtr.Zero)window=automation.FromHandle(handle).AsWindow();else Thread.Sleep(100);}while(window is null&&DateTime.UtcNow<deadline);
   Assert.NotNull(window);window.Focus();
   Wait(window,"Live workflows - read only");WaitLiveStatus(window);
   foreach(var period in new[]{"Weekly","Monthly","Daily"}){Wait(window,period).AsButton().Invoke();WaitLiveStatus(window,period);}
   Wait(window,"Workflow Pipeline");
   Wait(window,"Workflow selection").AsButton().Invoke();
   var selectionDeadline=DateTime.UtcNow.AddSeconds(10);FlaUI.Core.AutomationElements.AutomationElement? choice=null;
   do {choice=window.FindAllDescendants().FirstOrDefault(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button && (e.Properties.Name.ValueOrDefault??"").Contains("Pipeline") && (e.Properties.Name.ValueOrDefault??"").Split(' ').Last().Length==32);if(choice is null)Thread.Sleep(100);}while(choice is null&&DateTime.UtcNow<selectionDeadline);
   Assert.NotNull(choice);choice.AsButton().Invoke();WaitLiveStatus(window);
   Wait(window,"Workflow").AsButton().Invoke();Wait(window,"Inspect Workflow").AsButton().Invoke();Wait(window,"WorkflowRevision");
   Wait(window,"ITI Signal").AsButton().Invoke();Wait(window,"Inspect ITI Signal");Wait(window,"ITI Signal").AsButton().Invoke();
   Wait(window,"Workflow Pipeline").AsButton().Invoke();Wait(window,"Regime Discovery").AsButton().Invoke();Wait(window,"Inspect Regime Discovery").AsButton().Invoke();Wait(window,"ProcessingStatus");
   Wait(window,"Reconnect / reload").AsButton().Invoke();Thread.Sleep(500);WaitLiveStatus(window);Wait(window,"Workflow Pipeline");
   Wait(window,"Strategy Updates").AsButton().Invoke();Wait(window,"Load more workflows");Wait(window,"Futures Price");
   Wait(window,"Summary").AsButton().Invoke();
   Wait(window,"Summary is not available.");
   window.FindAllDescendants(cf=>cf.ByName("Details")).Last().AsButton().Invoke();Wait(window,"Workflow Pipeline");
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);
   File.WriteAllText(Path.Combine(dir,"live-native-uia.txt"),string.Join(Environment.NewLine,window.FindAllDescendants().Select(e=>$"{e.ControlType}: {e.Properties.Name.ValueOrDefault}")));
   window.Capture().Save(Path.Combine(dir,"live-strategy-details.png"));
   app.Close();deadline=DateTime.UtcNow.AddSeconds(10);while(!app.HasExited&&DateTime.UtcNow<deadline)Thread.Sleep(100);Assert.True(app.HasExited);
   AssertBackendReady();
  }finally {if(!app.HasExited)app.Kill();Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static void WaitLiveStatus(Window window,string? period=null)
 {
  var deadline=DateTime.UtcNow.AddSeconds(30);
  do {if(window.FindAllDescendants().Any(e=>{var name=e.Properties.Name.ValueOrDefault??"";return name.StartsWith("Intrinsic Time "+(period is null?"":period+":"))&&name.Contains("Live")&&name.Contains("workflows");}))return;Thread.Sleep(100);}while(DateTime.UtcNow<deadline);
  throw new TimeoutException("Native live status did not confirm workflow initialization/reload completion");
 }
 static FlaUI.Core.AutomationElements.AutomationElement Wait(Window window,string name)
 {
  var deadline=DateTime.UtcNow.AddSeconds(30);
  do {var element=window.FindFirstDescendant(cf=>cf.ByName(name));if(element is not null)return element;Thread.Sleep(100);}while(DateTime.UtcNow<deadline);
  throw new TimeoutException("Live native UI did not show: "+name);
 }

}

