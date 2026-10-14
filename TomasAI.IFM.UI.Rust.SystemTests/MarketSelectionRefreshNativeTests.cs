using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
public sealed class MarketSelectionRefreshNativeTests
{
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [Fact]public void Live_quote_polling_does_not_disable_or_flash_the_blotter(){
  SetThreadDpiAwarenessContext(new(-4));var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
  using var record=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));using var process=Process.GetProcessById(record.RootElement.GetProperty("RustProcessId").GetInt32());using var automation=new UIA3Automation();var window=automation.FromHandle(process.MainWindowHandle).AsWindow();window.Focus();
  if(window.FindFirstDescendant(cf=>cf.ByName("Trade Order Editor")) is null)Wait(window,"Trade Orders").AsButton().Invoke();
  Wait(window,"Trade Blotter").AsButton().Invoke();Wait(window,"Market Selection").AsButton().Invoke();
  var feed=Wait(window,"Live Feed").AsCheckBox();if(feed.IsChecked!=true)feed.Click();
  Wait(window,"Live Feed");
  Wait(window,"Selected legs: 4 / 4     Click Call or Put side to stage a leg");
  Thread.Sleep(500);
  var timer=Stopwatch.StartNew();int samples=0,disabled=0;bool polling=false;
  while(timer.Elapsed<TimeSpan.FromSeconds(8)){
   try{
    var control=window.FindFirstDescendant(cf=>cf.ByName("Live Feed"));
    if(control is not null){samples++;if(!control.IsEnabled)disabled++;Assert.True(control.AsCheckBox().IsChecked);}
    Assert.Null(window.FindFirstDescendant(cf=>cf.ByName("Pending")));
    try{using var metric=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"trade-control-geometry.json")));polling|=metric.RootElement.GetProperty("backgroundRefresh").GetBoolean();}catch(JsonException){}catch(IOException){}
   }catch(System.Runtime.InteropServices.COMException){}
   Thread.Sleep(40);
  }
  File.WriteAllText(Path.Combine(output,"blotter-live-refresh-observation.json"),JsonSerializer.Serialize(new{samples,disabled,polling}));
  FlaUI.Core.Capturing.Capture.Rectangle(window.BoundingRectangle).ToFile(Path.Combine(output,"blotter-live-nonflashing.png"));
  Assert.True(samples>=20);Assert.True(polling,"The check must observe an actual background refresh");Assert.Equal(0,disabled);Assert.True(process.Responding);
  Assert.Contains(new[]{"-SC","-SP","+LC","+LP"},role=>window.FindAllDescendants(cf=>cf.ByName(role)).Length>=2);
 }
 static AutomationElement Wait(Window window,string name){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(60)){var item=window.FindFirstDescendant(cf=>cf.ByName(name));if(item is not null&&item.IsEnabled)return item;Thread.Sleep(100);}throw new TimeoutException("Missing enabled "+name);}
}
