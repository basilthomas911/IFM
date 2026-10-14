using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
public sealed class FrozenEmulatorNativeTests
{
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [Fact]public void Frozen_strategy_is_visible_with_live_feed_unchecked(){
  SetThreadDpiAwarenessContext(new(-4));var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
  using var record=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));using var process=Process.GetProcessById(record.RootElement.GetProperty("RustProcessId").GetInt32());using var automation=new UIA3Automation();var window=automation.FromHandle(process.MainWindowHandle).AsWindow();window.Focus();
  if(window.FindFirstDescendant(cf=>cf.ByName("Trade Order Editor")) is null)Wait(window,"Trade Orders").AsButton().Invoke();
  Wait(window,"Trade Blotter").AsButton().Invoke();Wait(window,"Market Selection").AsButton().Invoke();
  var watch=Stopwatch.StartNew();bool selected=false;
  while(watch.Elapsed<TimeSpan.FromSeconds(45)){
   try{selected=window.FindFirstDescendant(cf=>cf.ByName("Selected legs: 4 / 4     Click Call or Put side to stage a leg")) is not null;}
   catch(System.Runtime.InteropServices.COMException){ /* AccessKit can replace nodes during an async publication. */ }
   if(selected)break;Thread.Sleep(100);
  }
  Assert.True(selected,"Four frozen strategy legs should be shown");Assert.False(Wait(window,"Live Feed").AsCheckBox().IsChecked);Assert.True(process.Responding);
  Thread.Sleep(200);
  var strikeHeaders=window.FindAllDescendants(cf=>cf.ByName("Strike"));Assert.Equal(2,strikeHeaders.Length);
  var centers=strikeHeaders.Select(x=>x.BoundingRectangle.Left+x.BoundingRectangle.Width/2).ToArray();
  Assert.InRange(Math.Abs(centers[0]-centers[1]),0,1.5);
  FlaUI.Core.Capturing.Capture.Rectangle(window.BoundingRectangle).ToFile(Path.Combine(output,"blotter-rust-frozen-selection.png"));
 }
 static AutomationElement Wait(Window window,string name){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(45)){var item=window.FindFirstDescendant(cf=>cf.ByName(name));if(item is not null&&item.IsEnabled)return item;Thread.Sleep(100);}throw new TimeoutException("Missing enabled "+name);}
}
