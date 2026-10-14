using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
public sealed class TradeOrderNativeControlParityTests
{
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [Fact]public void Native_controls_match_captured_WinForms_positions_and_calendar_opens(){
  SetThreadDpiAwarenessContext(new(-4));var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
  using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));using var process=Process.GetProcessById(state.RootElement.GetProperty("RustProcessId").GetInt32());using var automation=new UIA3Automation();var window=automation.FromHandle(process.MainWindowHandle).AsWindow();window.Focus();
  if(window.FindFirstDescendant(cf=>cf.ByName("Trade Order Editor")) is null)Wait(window,"Trade Orders").AsButton().Invoke();
  var editor=Wait(window,"Trade Order Editor");Wait(window,"Order Status");Assert.True(Wait(window,"Create Order...").IsEnabled);Assert.Null(window.FindFirstDescendant(cf=>cf.ByName("Completed")));var orderHeader=Wait(editor.AsWindow(),"Order Id");var referenceHeader=Wait(editor.AsWindow(),"Reference");var sourceHeader=Wait(editor.AsWindow(),"Source");Assert.InRange(referenceHeader.BoundingRectangle.X-orderHeader.BoundingRectangle.X,310,320);Assert.InRange(sourceHeader.BoundingRectangle.X-referenceHeader.BoundingRectangle.X,795,805);
  using var metrics=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"trade-control-geometry.json")));var scale=metrics.RootElement.GetProperty("pixelsPerPoint").GetDouble();var controls=metrics.RootElement.GetProperty("controls");var button=Wait(window,"Create Order...").BoundingRectangle;var originX=button.X-controls.GetProperty("btnCreateOrder")[0].GetDouble()*scale;var originY=button.Y-controls.GetProperty("btnCreateOrder")[1].GetDouble()*scale;var bounds=editor.BoundingRectangle;
  using var reference=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"trade-order-control-reference.json")));var differences=new List<object>();
  foreach(var original in reference.RootElement.GetProperty("Controls").EnumerateArray()){
   var id=original.GetProperty("Id").GetString()!;if(!controls.TryGetProperty(id,out var native))continue;var actual=new[]{native[0].GetDouble()*scale+originX-bounds.X,native[1].GetDouble()*scale+originY-bounds.Y,native[2].GetDouble()*scale,native[3].GetDouble()*scale};var expected=new[]{original.GetProperty("X").GetDouble(),original.GetProperty("Y").GetDouble(),original.GetProperty("Width").GetDouble(),original.GetProperty("Height").GetDouble()};var delta=actual.Select((v,i)=>v-expected[i]).ToArray();differences.Add(new{id,actual,expected,delta});foreach(var difference in delta)Assert.InRange(Math.Abs(difference),0,3);
  }
  File.WriteAllText(Path.Combine(output,"trade-control-parity.json"),JsonSerializer.Serialize(differences,new JsonSerializerOptions{WriteIndented=true}));window.Focus();Thread.Sleep(300);FlaUI.Core.Capturing.Capture.Rectangle(window.BoundingRectangle).ToFile(Path.Combine(output,"trade-control-parity.png"));
  Wait(window,"From calendar").AsButton().Invoke();Wait(window,"Mo");Wait(window,"Cancel").AsButton().Invoke();Assert.True(process.Responding);
 }
 static AutomationElement Wait(Window window,string name){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(20)){var item=window.FindFirstDescendant(cf=>cf.ByName(name));if(item is not null)return item;Thread.Sleep(100);}throw new TimeoutException("Missing "+name);}
}
