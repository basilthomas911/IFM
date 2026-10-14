using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
public sealed class DevelopmentHostPairTests
{
 [Fact]public async Task Launcher_owned_frontends_have_rendered_windows_and_leave_backend_ready()
 {
  var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")??throw new InvalidOperationException("Set the repository root.");var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};Assert.True((await http.GetAsync("http://localhost:22543/health/launch-ready")).IsSuccessStatusCode);
  using var automation=new UIA3Automation();foreach(var (field,exe,labels) in new[]{("WinFormsProcessId","TomasAI.IFM.UI.Net.exe",new[]{"Reference","Portfolio","System"}),("RustProcessId","ifm-ui-rust.exe",new[]{"Trade Orders","Reference","Portfolio","System","Strategy","Market Outlook"})})
  {
   using var process=Process.GetProcessById(state.RootElement.GetProperty(field).GetInt32());Assert.False(process.HasExited);Assert.Equal(exe,Path.GetFileName(process.MainModule!.FileName));var watch=Stopwatch.StartNew();IntPtr handle=IntPtr.Zero;while(handle==IntPtr.Zero&&watch.Elapsed<TimeSpan.FromSeconds(30)){process.Refresh();handle=process.MainWindowHandle;Thread.Sleep(50);}Assert.NotEqual(IntPtr.Zero,handle);var window=automation.FromHandle(handle).AsWindow();foreach(var label in labels){watch.Restart();while(!HasLabel(window,label)&&watch.Elapsed<TimeSpan.FromSeconds(20))Thread.Sleep(50);Assert.True(HasLabel(window,label),"Missing frontend navigation: "+label);}window.Capture().Save(Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!,field+"-running.png"));Assert.True(process.Responding);
  }
  Assert.True((await http.GetAsync("http://localhost:22543/health/launch-ready")).IsSuccessStatusCode);
 }
 static bool HasLabel(Window window,string label){try{return window.FindFirstDescendant(cf=>cf.ByName(label)) is not null;}catch(System.Runtime.InteropServices.COMException){return false;}catch(FlaUI.Core.Exceptions.ElementNotAvailableException){return false;}}
}
