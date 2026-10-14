using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
public sealed class TradeMouseInputNativeTests {
[System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
[System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int n);
[System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
[System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
[System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
[System.Runtime.InteropServices.DllImport("user32.dll")]static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
static void Click(AutomationElement e){var r=e.BoundingRectangle;SetCursorPos((int)(r.X+r.Width/2),(int)(r.Y+r.Height/2));Thread.Sleep(150);mouse_event(2,0,0,0,UIntPtr.Zero);Thread.Sleep(80);mouse_event(4,0,0,0,UIntPtr.Zero);}
[Fact] public void Mouse_clicks_switch_blotter_tabs_during_live_updates(){
SetThreadDpiAwarenessContext(new(-4));
var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
using var record=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));using var p=Process.GetProcessById(record.RootElement.GetProperty("RustProcessId").GetInt32());using var a=new UIA3Automation();var w=a.FromHandle(p.MainWindowHandle).AsWindow();SetWindowPos(p.MainWindowHandle,new(-1),0,0,0,0,0x43);ShowWindow(p.MainWindowHandle,3);SetForegroundWindow(p.MainWindowHandle);w.Focus();Thread.Sleep(500);
if(w.FindFirstDescendant(cf=>cf.ByName("Trade Order Editor")) is null)Wait(w,"Trade Orders").AsButton().Invoke();
Wait(w,"Trade Blotter").AsButton().Invoke();Wait(w,"Market Selection").AsButton().Invoke();var feed=Wait(w,"Live Feed").AsCheckBox();if(feed.IsChecked!=true)Click(feed);
Wait(w,"Selected legs: 4 / 4     Click Call or Put side to stage a leg");
for(var i=0;i<5;i++){
var fills=Wait(w,"Order Fills");File.WriteAllText(Path.Combine(output,"mouse-click-bounds.txt"),$"window={w.BoundingRectangle}; fills={fills.BoundingRectangle}; hwnd={p.MainWindowHandle}");Click(fills);WaitTab(output,2);Wait(w,"Cancel Unfilled",false);
Click(Wait(w,"Market Selection"));WaitTab(output,0);Wait(w,"Live Feed");Thread.Sleep(250);
}
SetWindowPos(p.MainWindowHandle,new(-2),0,0,0,0,3);
File.WriteAllText(Path.Combine(output,"mouse-input-state.txt"),string.Join("\n",w.FindAllDescendants().Select(e=>$"{e.Properties.ControlType.ValueOrDefault} | {e.Properties.Name.ValueOrDefault} | enabled={e.Properties.IsEnabled.ValueOrDefault}")));
FlaUI.Core.Capturing.Capture.Rectangle(w.BoundingRectangle).ToFile(Path.Combine(output,"mouse-input-state.png"));Assert.True(p.Responding);
}
static void WaitTab(string output,int expected){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(10)){try{using var metric=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"trade-control-geometry.json")));if(metric.RootElement.GetProperty("composeTab").GetInt32()==expected)return;}catch(IOException){}catch(JsonException){}Thread.Sleep(100);}throw new TimeoutException("Rust did not draw tab "+expected);}
static AutomationElement Wait(Window w,string name,bool enabled=true){var timer=Stopwatch.StartNew();while(timer.Elapsed<TimeSpan.FromSeconds(20)){var e=w.FindFirstDescendant(cf=>cf.ByName(name));if(e is not null&&(!enabled||e.IsEnabled))return e;Thread.Sleep(100);}FlaUI.Core.Capturing.Capture.Rectangle(w.BoundingRectangle).ToFile(Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!,"mouse-failure.png"));throw new TimeoutException("Missing "+name);}
}
