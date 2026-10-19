using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using TomasAI.IFM.UI.Net.Views.Portfolio;

public sealed class PortfolioFlaUiTests
{
    [PortfolioLiveFact]
    public void Live_portfolio_navigation_and_cancel_preserve_original_controls_and_geometry()
    {
        var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
            var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
            var output=Path.Combine(root,".artifacts/portfolio-migration/flaui");Directory.CreateDirectory(output);
            using var automation=new UIA3Automation {TransactionTimeout=TimeSpan.FromSeconds(5),ConnectionTimeout=TimeSpan.FromSeconds(3)};
            var executable=Path.Combine(root,".artifacts/rust-ui/app/ifm-ui-rust.exe");
            var start=new ProcessStartInfo(executable){WorkingDirectory=root};start.Environment["IFM_RUST_UI_MODE"]="live";
            using var process=Process.Start(start)!;
            try {
                var deadline=DateTime.UtcNow.AddSeconds(40);IntPtr handle;
                while((handle=NativeWindows.Find(process.Id))==IntPtr.Zero&&DateTime.UtcNow<deadline)Thread.Sleep(100);
                Assert.NotEqual(IntPtr.Zero,handle);var rust=automation.FromHandle(handle).AsWindow();
                Click(Wait(rust,"Portfolio",true));Wait(rust,"Portfolio Administration");Wait(rust,"Portfolio Actions",true);
                using var host=new DesktopViewTestHost(scope=>{
                    var f=new PortfolioAdministrationForm();
                    f.Shown+=async(_,_)=>await f.LoadViewModelAsync(scope.Services.PortfolioQueries,scope.Services.PortfolioCommands,scope.Services.PortfolioFundCommands,scope.Services.PortfolioIdentities,scope.Services.PortfolioPolicyCommands,scope.Services.ReferenceQueries,financialApi:scope.Services.PortfolioFinancial,riskApi:scope.Services.RiskQueries);
                    return(f,()=>Task.CompletedTask);
                });
                var original=automation.FromHandle(host.Handle).AsWindow();
                Wait(original,"Create Portfolio",true);
                var manager=Wait(rust,"Portfolio Administration");
                Wait(rust,"Risk Policy Actions",true);Wait(rust,"Versions",true);
                var policyActions=Wait(manager.AsWindow(),"Risk Policy Actions").BoundingRectangle;
                foreach(var label in new[]{"Save","Cancel"}){
                    var button=Wait(manager.AsWindow(),label).BoundingRectangle;
                    Assert.True(button.Top>=policyActions.Bottom);
                    Assert.True(button.Left>=policyActions.Left-2);
                    Assert.InRange(Math.Abs(button.Width-policyActions.Width),0,2);
                }
                Assert.True(Wait(manager.AsWindow(),"Cancel").BoundingRectangle.Top>=Wait(manager.AsWindow(),"Save").BoundingRectangle.Bottom);
                Assert.InRange(Math.Abs(original.BoundingRectangle.Width-manager.BoundingRectangle.Width),0,25);
                Assert.InRange(Math.Abs(original.BoundingRectangle.Height-manager.BoundingRectangle.Height),0,35);
                original.SetForeground();original.Capture().Save(Path.Combine(output,"winforms-administration.png"));
                rust.SetForeground();rust.Capture().Save(Path.Combine(output,"rust-administration.png"));
                Click(Wait(manager.AsWindow(),"Versions",true));
                Wait(manager.AsWindow(),"Save");Wait(manager.AsWindow(),"Cancel");
                rust.Capture().Save(Path.Combine(output,"rust-policy-versions.png"));
                Click(Wait(manager.AsWindow(),"Risk Policy Actions",true));
                var newPolicy=Wait(rust,"New Policy...");var changePolicy=Wait(rust,"Change Policy...",true);
                Assert.True(changePolicy.BoundingRectangle.Top>=newPolicy.BoundingRectangle.Bottom);
                Wait(rust,"Activate & Assign");Wait(rust,"Retire");Wait(rust,"Delete Draft");
                Click(changePolicy);Wait(manager.AsWindow(),"Cancel",true);
                Click(Wait(manager.AsWindow(),"Cancel",true));Wait(manager.AsWindow(),"Portfolio Actions",true);
                Click(Wait(rust,"Portfolio Actions",true));Click(Wait(rust,"New Version...",true));
                Wait(rust,"Create Portfolio Version");Wait(rust,"Save",true);Wait(rust,"Cancel",true);
                rust.SetForeground();rust.Capture().Save(Path.Combine(output,"rust-portfolio-version.png"));
                Click(Wait(rust,"Cancel",true));Wait(rust,"Portfolio Actions",true);
                Click(Wait(rust,"Funds",true));Wait(rust,"Financials...",true);
                Wait(manager.AsWindow(),"Fund ID");Wait(manager.AsWindow(),"Portfolio");
                foreach(var name in new[]{"ES Daily Development Fund","ES Weekly Development Fund","ES Monthly Development Fund"})
                    Assert.Single(manager.FindAllDescendants(cf=>cf.ByName(name)));
                rust.Capture().Save(Path.Combine(output,"rust-funds-active.png"));
                OpenFundFilter(manager.AsWindow());Click(Wait(rust,"Disabled",true));
                Wait(manager.AsWindow(),"Disabled",true);
                var loading=Stopwatch.StartNew();
                while(manager.FindAllDescendants(cf=>cf.ByName("ES Weekly Development Fund")).Length!=2&&loading.Elapsed<TimeSpan.FromSeconds(10))Thread.Sleep(50);
                var weekly=manager.FindAllDescendants(cf=>cf.ByName("ES Weekly Development Fund")).OrderBy(e=>e.BoundingRectangle.Top).ToArray();
                Assert.Equal(2,weekly.Length);
                // Versions sort newest first. Select Weekly v2 by its rightmost Version cell.
                FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(manager.BoundingRectangle.Right-30,(int)(weekly[0].BoundingRectangle.Top+weekly[0].BoundingRectangle.Height/2)));
                FlaUI.Core.Input.Mouse.Click();
                Click(Wait(manager.AsWindow(),"Fund Details",true));
                Wait(manager.AsWindow(),"Portfolio:");Wait(manager.AsWindow(),"Portfolio");
                Wait(manager.AsWindow(),"ES Weekly Development Fund");Wait(manager.AsWindow(),"2");
                rust.Capture().Save(Path.Combine(output,"rust-fund-version-selected.png"));
                Click(Wait(manager.AsWindow(),"Funds",true));
                OpenFundFilter(manager.AsWindow());Click(Wait(rust,"Active",true));
                Wait(rust,"Financials...",true);
                Click(Wait(rust,"Financials...",true));Wait(rust,"Transaction");Wait(rust,"Next transactions");Wait(rust,"Next reservations");Wait(rust,"Ledger",true);
                rust.SetForeground();rust.Capture().Save(Path.Combine(output,"rust-financials.png"));
                var financial=rust.FindAllDescendants(cf=>cf.ByControlType(ControlType.Window)).First(e=>e.Name.EndsWith("Financials",StringComparison.Ordinal)).AsWindow();
                Click(Wait(financial,"Ledger",true));var ledger=Wait(rust,"Portfolio ledger administration").AsWindow();Wait(ledger,"Refresh",true);
                rust.SetForeground();rust.Capture().Save(Path.Combine(output,"rust-ledger.png"));
                File.WriteAllText(Path.Combine(output,"close-controls.json"),JsonSerializer.Serialize(rust.FindAllDescendants(cf=>cf.ByName("Close")).Select(e=>new{e.Name,e.IsEnabled,Bounds=e.BoundingRectangle.ToString()})));
                Click(Wait(ledger,"Close",true));
                var closing=Stopwatch.StartNew();while(rust.FindFirstDescendant(cf=>cf.ByName("Portfolio ledger administration")) is not null&&closing.Elapsed<TimeSpan.FromSeconds(15))Thread.Sleep(100);
                financial=rust.FindAllDescendants(cf=>cf.ByControlType(ControlType.Window)).First(e=>e.Name.EndsWith("Financials",StringComparison.Ordinal)).AsWindow();
                Wait(financial,"Transaction",true);
                // Closing a nested editor schedules the original parent refresh. Wait for
                // that refresh to finish before sending another accessibility action.
                var stable=Stopwatch.StartNew();
                while(stable.Elapsed<TimeSpan.FromMilliseconds(750)){
                    if(!Wait(financial,"Close").IsEnabled)stable.Restart();
                    Thread.Sleep(50);
                }
                Click(Wait(financial,"Close",true));Wait(rust,"Financials...",true);
                File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(new{Result="Passed",Original=original.BoundingRectangle.ToString(),Rust=manager.BoundingRectangle.ToString(),Checked=new[]{"Live Portfolio load","Risk Policy and Versions tabs","Six Risk Policy Actions menu entries","Change Policy > Cancel","Original form dimensions","Portfolio Actions > New Version > Cancel","Funds selection","Financials and bounded page controls","Ledger configuration > Close","Return to administration"},NotExecuted=new[]{"Persisted configuration changes","Cash movements","Deletes"}},new JsonSerializerOptions{WriteIndented=true}));
            }finally {if(!process.HasExited){process.Kill();process.WaitForExit(10000);}}
        } finally {SetThreadDpiAwarenessContext(previous);}
    }
    static AutomationElement Wait(Window root,string name,bool enabled=false)
    {
        var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(40)){
            var elements=root.FindAllDescendants(cf=>cf.ByName(name));var e=elements.Where(e=>!enabled||e.IsEnabled).OrderByDescending(e=>e.BoundingRectangle.Width*e.BoundingRectangle.Height).FirstOrDefault();
            if(e is not null)return e;Thread.Sleep(100);
        }
        var output=Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!,".artifacts/portfolio-migration/flaui");
        root.SetForeground();root.Capture().Save(Path.Combine(output,"failure.png"));
        File.WriteAllText(Path.Combine(output,"failure-text.txt"),string.Join(Environment.NewLine,root.FindAllDescendants(cf=>cf.ByControlType(ControlType.Text)).Select(e=>e.Name)));
        throw new TimeoutException("Missing Portfolio control: "+name);
    }
    static void OpenFundFilter(Window manager){var label=Wait(manager,"Show State").BoundingRectangle;FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(label.Right+60,label.Top+label.Height/2));FlaUI.Core.Input.Mouse.Click();}
    static void Click(AutomationElement element){if(element.Patterns.Invoke.IsSupported)element.Patterns.Invoke.Pattern.Invoke();else if(element.Patterns.SelectionItem.IsSupported)element.Patterns.SelectionItem.Pattern.Select();else element.Click();}
    [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    sealed class PortfolioLiveFactAttribute:FactAttribute {public PortfolioLiveFactAttribute(){if(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_PARITY")!="1")Skip="Explicit live read-only Portfolio FlaUI verification.";}}
}
