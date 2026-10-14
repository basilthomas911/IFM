using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.Domain.Trade.Shared;
using System.Text.Json;
public sealed class TradeOrderControlReferenceTests
{
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [Fact]public void Capture_runtime_control_rectangles_from_original_WinForms_layout(){
  var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
  SetThreadDpiAwarenessContext(new(-4));
  using var host=new DesktopViewTestHost(scope=>{SetThreadDpiAwarenessContext(new(-4));var reference=scope.Container.GetInstance<IReferenceDataService>();var model=new TradeOrderEditorViewModel(scope,scope.ValueDates.ValueDate,[],reference,brokerEnvironment:BrokerEnvironment.Emulator);var form=new TradeOrderEditorForm(scope,reference);form.FitToTradeView(new(764,60,1912,1301));form.TopMost=true;form.LoadViewModel(model);return(form,()=>model.DisposeAsync().AsTask());});
  using var automation=new UIA3Automation();var window=automation.FromHandle(host.Handle).AsWindow();window.Focus();Thread.Sleep(500);
  var records=window.FindAllDescendants().Where(e=>!string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault)).Select(e=>new{Id=e.Properties.AutomationId.ValueOrDefault,Name=e.Properties.Name.ValueOrDefault,Type=e.ControlType.ToString(),X=e.BoundingRectangle.X-window.BoundingRectangle.X,Y=e.BoundingRectangle.Y-window.BoundingRectangle.Y,Width=e.BoundingRectangle.Width,Height=e.BoundingRectangle.Height}).ToArray();
  File.WriteAllText(Path.Combine(output,"trade-order-control-reference.json"),JsonSerializer.Serialize(new{Window=window.BoundingRectangle.ToString(),Controls=records},new JsonSerializerOptions{WriteIndented=true}));
  Assert.Contains(records,r=>r.Id=="dtpFrom");Assert.Contains(records,r=>r.Id=="btnCreateOrder");window.Capture().Save(Path.Combine(output,"trade-order-control-reference.png"));
 }
}
