using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using NativeApp=FlaUI.Core.Application;
/// <summary>Drives only the explicitly supplied test process. The caller must
/// configure an isolated loopback broker; no Development/live backend is allowed.</summary>
internal static class Program{
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
 [STAThread] static int Main(string[] args){
  _=SetThreadDpiAwarenessContext(new IntPtr(-4));
  if(args.Length!=3)throw new ArgumentException("Expected executable, scenario JSON, artifact directory");
  var url=Environment.GetEnvironmentVariable("IFM_UI_NATS_URL");
  if(!Uri.TryCreate(url,UriKind.Absolute,out var broker)||!broker.IsLoopback||broker.Port==4222)throw new InvalidOperationException("Desktop acceptance requires an isolated loopback broker");
  var exe=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);
  using(var binary=File.OpenRead(exe))File.WriteAllText(Path.Combine(output,"executable.json"),JsonSerializer.Serialize(new{path=exe,bytes=binary.Length,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(binary)),startedEastern=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).ToString("O")}));
  var launch=new ProcessStartInfo(exe){WorkingDirectory=Path.GetDirectoryName(exe)!,UseShellExecute=false};
  if(Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_CONFIG_ONLY")=="1"&&Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_EXPECT_NATIVE")=="1"){
   var profile=Path.GetFullPath(Environment.GetEnvironmentVariable("IFM_UI_CONFIG_PATH")??throw new InvalidOperationException("Missing test-owned client profile"));
   if(!profile.StartsWith(output+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Configuration acceptance requires a profile inside this test's artifact directory");
   using var config=JsonDocument.Parse(File.ReadAllText(profile));
   if(config.RootElement.GetProperty("AppSettings").GetProperty("NatsServerUri").GetString()!=url||config.RootElement.GetProperty("MarketDataHost").GetProperty("NatsUrl").GetString()!=url||config.RootElement.GetProperty("MarketDataHost").GetProperty("Scope").GetString()!="nativecompat"||config.RootElement.GetProperty("TradeBroker").GetProperty("Environment").GetString()!="Emulator")throw new InvalidOperationException("Client profile must target the isolated emulator fixture");
   if(!Uri.TryCreate(config.RootElement.GetProperty("MarketDataOperationsHealth").GetProperty("Endpoint").GetString(),UriKind.Absolute,out var health)||!health.IsLoopback||health.Port==22543)throw new InvalidOperationException("Client profile health must target the isolated HTTP fixture");
   foreach(var key in new[]{"IFM_UI_NATS_URL","IFM_UI_MARKETDATA_NATS_URL","IFM_UI_MARKETDATA_SCOPE","IFM_UI_ACTOR_HEALTH_URL","IFM_UI_OPERATIONS_HEALTH_URL","IFM_UI_LIVE_PIPELINE_HEALTH_URL","IFM_UI_BROKER_ACCOUNT_ALIAS","IFM_UI_BROKER_ENVIRONMENT","TradeBroker__AccountAlias","TradeBroker__Environment","TradeBroker__Emulator__AccountAlias","SupervisorControl__Enabled","SupervisorControl__Requester","SupervisorControl__Timeout"})launch.Environment.Remove(key);
  }
  using var automation=new UIA3Automation{TransactionTimeout=TimeSpan.FromSeconds(3),ConnectionTimeout=TimeSpan.FromSeconds(3)};using var app=NativeApp.Launch(launch);using var process=Process.GetProcessById(app.ProcessId);Window? window=null;
  try{
   // Eframe creates a tiny helper window before the actual viewport. Never
   // accept that helper as the desktop under test.
   var timer=Stopwatch.StartNew();while(window is null&&timer.Elapsed<TimeSpan.FromSeconds(30)){process.Refresh();if(app.HasExited)throw new InvalidOperationException("Desktop exited during startup");window=app.GetAllTopLevelWindows(automation).FirstOrDefault(w=>w.Properties.ProcessId.ValueOrDefault==app.ProcessId&&w.BoundingRectangle.Width>=600&&w.BoundingRectangle.Height>=400);if(window is null)Thread.Sleep(100);}
   if(window is null)throw new TimeoutException("Desktop window not found");
   window.SetForeground();
   var modules=process.Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToArray();
   File.WriteAllText(Path.Combine(output,"process-modules.json"),JsonSerializer.Serialize(modules));
   if(Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_EXPECT_NATIVE")=="1"&&modules.Any(n=>n is not null&&(n.Equals("coreclr.dll",StringComparison.OrdinalIgnoreCase)||n.Equals("hostfxr.dll",StringComparison.OrdinalIgnoreCase)||n.Equals("hostpolicy.dll",StringComparison.OrdinalIgnoreCase))))throw new InvalidOperationException("Standalone process loaded a managed runtime");
   using var plan=JsonDocument.Parse(File.ReadAllText(args[1]));
   foreach(var step in plan.RootElement.EnumerateArray()){
    var action=step.GetProperty("action").GetString()!;var name=step.TryGetProperty("name",out var n)?n.GetString()!:"";var index=step.TryGetProperty("index",out var i)?i.GetInt32():0;var type=step.TryGetProperty("type",out var t)?t.GetString():null;bool? enabled=step.TryGetProperty("enabled",out var flag)?flag.GetBoolean():null;
    File.AppendAllText(Path.Combine(output,"steps.log"),$"{DateTimeOffset.UtcNow:O} {action} {name}\n");
    var searchRoot=step.TryGetProperty("scope",out var parent)?Wait(window,parent.GetString()!,0,false,"Window"):window;
    if(action=="close"){
     Dump(window,output,"before-close");process.Refresh();var finalModules=process.Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToArray();File.WriteAllText(Path.Combine(output,"process-modules-final.json"),JsonSerializer.Serialize(finalModules));
     if(Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_EXPECT_NATIVE")=="1"&&finalModules.Any(n=>n is not null&&(n.Equals("coreclr.dll",StringComparison.OrdinalIgnoreCase)||n.Equals("hostfxr.dll",StringComparison.OrdinalIgnoreCase)||n.Equals("hostpolicy.dll",StringComparison.OrdinalIgnoreCase))))throw new InvalidOperationException("Standalone loaded a managed runtime during the scenario");
     window.Close();if(!process.WaitForExit(15000))throw new TimeoutException("Desktop did not exit after closing its window");File.WriteAllText(Path.Combine(output,"shutdown.txt"),"Graceful process exit observed");return 0;
    }
    if(action=="snapshot"){Dump(window,output,name,step.TryGetProperty("scope",out var scope)?scope.GetString():null);continue;}
    if(action=="parents"){
     var node=Wait(searchRoot,name,index,false,type,enabled);var parents=new List<object>();for(var depth=0;node is not null&&depth<10&&node.Properties.ProcessId.ValueOrDefault==app.ProcessId;depth++,node=node.Parent){parents.Add(new{depth,type=node.ControlType.ToString(),name=node.Name,bounds=node.BoundingRectangle.ToString(),children=node.FindAllChildren().Select(e=>new{type=e.ControlType.ToString(),name=e.Name}).Take(12).ToArray()});}
     File.WriteAllText(Path.Combine(output,"parents.json"),JsonSerializer.Serialize(parents,new JsonSerializerOptions{WriteIndented=true}));continue;
    }
    if(action=="control"){
     var oracle=Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_CONTROL_ORACLE")??throw new InvalidOperationException("Missing isolated control tool");
     var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in new[]{oracle,"--control",url!,step.GetProperty("verb").GetString()!,step.GetProperty("value").GetString()!})start.ArgumentList.Add(argument);
     using var control=Process.Start(start)!;if(!control.WaitForExit(15000)){control.Kill();throw new TimeoutException("Isolated control tool did not finish");}if(control.ExitCode!=0)throw new InvalidOperationException(control.StandardError.ReadToEnd());File.AppendAllText(Path.Combine(output,"controls.log"),control.StandardOutput.ReadToEnd());continue;
    }
    if(action=="wait"){_ = Wait(searchRoot,name,index,step.TryGetProperty("contains",out var c)&&c.GetBoolean(),type,enabled);continue;}
    if(action=="invoke"){var element=Wait(searchRoot,name,index,false,type,enabled);if(!element.IsEnabled)throw new InvalidOperationException("Control is disabled: "+name);if(element.Patterns.Invoke.IsSupported)element.Patterns.Invoke.Pattern.Invoke();else ClickOwned(element,window,app.ProcessId);continue;}
    if(action=="date-picker"){
     // Select from observed date buttons, whose default legitimately differs
     // between the API-clock client and the unchanged workstation-clock client.
     var dates=searchRoot.FindAllDescendants().Where(e=>e.ControlType==FlaUI.Core.Definitions.ControlType.Button&&System.Text.RegularExpressions.Regex.IsMatch(e.Properties.Name.ValueOrDefault??"",@"^\d{4}-\d{2}-\d{2} \uD83D\uDCC6$")).OrderBy(e=>e.BoundingRectangle.Top).ToArray();
     var date=dates.ElementAtOrDefault(index)??throw new InvalidOperationException("Date picker not found in the requested scope");
     if(!date.IsEnabled)throw new InvalidOperationException("Date picker is disabled");
     if(date.Patterns.Invoke.IsSupported)date.Patterns.Invoke.Pattern.Invoke();else ClickOwned(date,window,app.ProcessId);continue;
    }
    if(action=="click"){ClickOwned(Wait(searchRoot,name,index,false,type,enabled),window,app.ProcessId);continue;}
    if(action=="expand"){var element=Wait(searchRoot,name,index,false,type,enabled);File.AppendAllText(Path.Combine(output,"controls.log"),$"Expand {name}: invoke={element.Patterns.Invoke.IsSupported}, expand={element.Patterns.ExpandCollapse.IsSupported}\n");if(element.Patterns.Invoke.IsSupported)element.Patterns.Invoke.Pattern.Invoke();else if(element.Patterns.ExpandCollapse.IsSupported)element.Patterns.ExpandCollapse.Pattern.Expand();else ClickOwned(element,window,app.ProcessId);continue;}
    if(action=="text"){
     var element=Wait(searchRoot,name,index,false,type,enabled);var desired=step.GetProperty("value").GetString()!;element.AsTextBox().Text=desired;Thread.Sleep(150);
     if(element.AsTextBox().Text!=desired){ClickOwned(element,window,app.ProcessId);FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);FlaUI.Core.Input.Keyboard.Type(desired);}
     var entered=Stopwatch.StartNew();while(element.AsTextBox().Text!=desired&&entered.Elapsed<TimeSpan.FromSeconds(5))Thread.Sleep(50);if(element.AsTextBox().Text!=desired)throw new InvalidOperationException("The UI did not retain the entered value for "+name);continue;
    }
    if(action=="pause"){Thread.Sleep(Math.Min(2000,step.GetProperty("milliseconds").GetInt32()));continue;}
    throw new InvalidDataException("Unknown desktop acceptance action: "+action);
   }
   Dump(window,output,"final");return 0;
  }catch(Exception error){File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());if(window is not null)Dump(window,output,"failure");return 1;}
  finally{if(!app.HasExited)app.Kill();}
 }
 static void ClickOwned(AutomationElement element,Window window,int processId){window.SetForeground();Thread.Sleep(150);_=GetWindowThreadProcessId(GetForegroundWindow(),out var foreground);if(foreground!=(uint)processId)throw new InvalidOperationException("The test application cannot obtain foreground focus; physical input was withheld");element.Click();}
 static AutomationElement Wait(AutomationElement window,string name,int index,bool contains,string? type=null,bool? enabled=null){
  var timer=Stopwatch.StartNew();while(timer.Elapsed<TimeSpan.FromSeconds(20)){
   var candidates=contains||name.Length==0?window.FindAllDescendants():window.FindAllDescendants(cf=>cf.ByName(name));
   var found=candidates.Where(e=>(type is null||e.ControlType.ToString()==type)&&(!enabled.HasValue||e.IsEnabled==enabled.Value)&&(name.Length!=0||string.IsNullOrEmpty(e.Properties.Name.ValueOrDefault))&&(!contains||(e.Properties.Name.ValueOrDefault??"").Contains(name,StringComparison.Ordinal))).Skip(index).FirstOrDefault();
   if(found is not null)return found;Thread.Sleep(50);
  }throw new TimeoutException("Missing UI control: "+name+" index "+index);
 }
 static void Dump(Window window,string directory,string name,string? scope=null){
  if(name.Length==0||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_'))throw new InvalidDataException("Invalid evidence name");
  var owner=scope is null?window:Wait(window,scope,0,false,"Window");
  var rows=owner.FindAllDescendants().Select(e=>new{type=e.ControlType.ToString(),name=e.Properties.Name.ValueOrDefault??"",id=e.Properties.AutomationId.ValueOrDefault??"",enabled=e.IsEnabled,offscreen=e.Properties.IsOffscreen.ValueOrDefault,value=e.Patterns.Value.IsSupported?e.Patterns.Value.Pattern.Value.Value:null,bounds=e.BoundingRectangle.ToString(),x=e.BoundingRectangle.Left-owner.BoundingRectangle.Left,y=e.BoundingRectangle.Top-owner.BoundingRectangle.Top,width=e.BoundingRectangle.Width,height=e.BoundingRectangle.Height}).ToArray();
  File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(directory,name+"-window.json"),JsonSerializer.Serialize(new{width=window.BoundingRectangle.Width,height=window.BoundingRectangle.Height}));
  window.SetForeground();Thread.Sleep(250);
  // Capture the owned HWND, not screen pixels from an application that may
  // cover it while the operator is using this shared desktop.
  try{using var bitmap=new System.Drawing.Bitmap(window.BoundingRectangle.Width,window.BoundingRectangle.Height);using(var graphics=System.Drawing.Graphics.FromImage(bitmap)){var dc=graphics.GetHdc();try{if(!PrintWindow(window.Properties.NativeWindowHandle.Value,dc,2))throw new InvalidOperationException("Owned-window capture failed");}finally{graphics.ReleaseHdc(dc);}}bitmap.Save(Path.Combine(directory,name+".png"),System.Drawing.Imaging.ImageFormat.Png);}catch(Exception error){File.WriteAllText(Path.Combine(directory,name+"-capture-error.txt"),error.ToString());throw;}
 }
}
