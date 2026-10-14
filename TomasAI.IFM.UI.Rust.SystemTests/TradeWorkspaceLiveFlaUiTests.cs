using System.Diagnostics;
using System.Text.Json;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.Domain.Trade.Shared;
public sealed class TradeWorkspaceLiveFlaUiTests
{
    [Fact]public void Both_live_editors_expose_portfolio_fund_orders_and_trade_actions_without_mutation()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        using var dpi=new DpiScope();
        using var host=new DesktopViewTestHost(scope=>{
            var reference=scope.Container.GetInstance<IReferenceDataService>();
            var model=new TradeOrderEditorViewModel(scope,scope.ValueDates.ValueDate,[],reference,brokerEnvironment:BrokerEnvironment.Emulator);
            var form=new TradeOrderEditorForm(scope,reference);form.LoadViewModel(model);form.Width=1150;form.Height=850;form.TopMost=true;
            return(form,()=>model.DisposeAsync().AsTask());
        });
        using var automation=new UIA3Automation();var winforms=automation.FromHandle(host.Handle).AsWindow();
        winforms.Focus();Thread.Sleep(300);Wait(winforms,"Trade Orders");Wait(winforms,"Trade Blotter");winforms.Capture().Save(Path.Combine(output,"trade-orders-winforms.png"));host.Dispose();
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var process=Process.GetProcessById(state.RootElement.GetProperty("RustProcessId").GetInt32());
        using var rustAutomation=new UIA3Automation();var rust=rustAutomation.FromHandle(process.MainWindowHandle).AsWindow();rust.Focus();Wait(rust,"Trade Orders").AsButton().Invoke();
        Wait(rust,"Trade Order Editor");Wait(rust,"Portfolio:");Wait(rust,"Fund:");Wait(rust,"Trade Blotter");
        Wait(rust,"Order Id");Wait(rust,"Trade Id");Wait(rust,"Add Trade...");Wait(rust,"Load Trade");
        Assert.True(process.Responding);rust.Capture().Save(Path.Combine(output,"trade-orders-rust.png"));
    }
    static AutomationElement Wait(Window w,string name){var sw=Stopwatch.StartNew();while(sw.Elapsed<TimeSpan.FromSeconds(40)){try{var e=w.FindAllDescendants().FirstOrDefault(e=>e.Properties.Name.ValueOrDefault==name);if(e is not null)return e;}catch(System.Runtime.InteropServices.COMException){}Thread.Sleep(100);}var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;w.Capture().Save(Path.Combine(output,"trade-layout-failure.png"));File.WriteAllLines(Path.Combine(output,"trade-layout-failure-tree.txt"),w.FindAllDescendants().Select(e=>e.Properties.Name.ValueOrDefault));throw new TimeoutException("Missing "+name);}
    sealed class DpiScope:IDisposable{readonly IntPtr previous=SetThreadDpiAwarenessContext(new(-4));public void Dispose()=>SetThreadDpiAwarenessContext(previous);}
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
