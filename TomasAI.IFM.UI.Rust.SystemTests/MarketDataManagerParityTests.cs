using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Reflection;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Views.MarketData;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

public sealed class MarketDataManagerParityTests
{
    [MarketDataParityFact]
    public void Inspect_actual_WinForms_and_Rust_market_data_managers()
    {
        using var dpi = new DpiScope();
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var output=Path.Combine(root,".artifacts/market-data-manager/flaui");Directory.CreateDirectory(output);
        using var automation=new UIA3Automation { TransactionTimeout=TimeSpan.FromSeconds(5), ConnectionTimeout=TimeSpan.FromSeconds(3) };
        var executable=Environment.GetEnvironmentVariable("IFM_MARKET_DATA_PARITY_EXE")??Path.Combine(root,".artifacts/rust-ui/app/ifm-ui-rust.exe");
        var start=new ProcessStartInfo(executable){WorkingDirectory=root};start.Environment["IFM_RUST_UI_MODE"]="live";
        using var process=Process.Start(start)!;
        var reports=new List<object>();
        try {
            var deadline=DateTime.UtcNow.AddSeconds(30);IntPtr handle;
            while((handle=NativeWindows.Find(process.Id))==IntPtr.Zero && DateTime.UtcNow<deadline)Thread.Sleep(100);
            Assert.NotEqual(IntPtr.Zero,handle);
            var rust=automation.FromHandle(handle).AsWindow();
            WaitName(rust,"Market Data",true).AsButton().Invoke();WaitName(rust,"Market Data Manager");WaitName(rust,"Add",true);
            for(var index=0;index<3;index++) {
                var selection=index;var label=new[]{"Futures Contracts","Futures Option Contracts","Yield Curve Rates"}[index];
                using var host=new DesktopViewTestHost(scope=>{
                    var reference=scope.Container.GetInstance<IReferenceDataService>();
                    var definitions=reference.GetMarketDataDefinitionTypesAsync().GetAwaiter().GetResult();
                    File.WriteAllText(Path.Combine(output,"definition-query.json"),JsonSerializer.Serialize(definitions));
                    var proxy=DispatchProxy.Create<IReferenceDataService,SelectorCatalogProxy>();
                    var fixture=(SelectorCatalogProxy)(object)proxy;fixture.Target=reference;fixture.First=selection;
                    var form=new MarketDataForm(scope,scope.Container.GetInstance<IStatusConsoleEventProducer>(),proxy);
                    form.LoadViewModel(new MarketDataViewModel(proxy));return(form,()=>Task.CompletedTask);
                });
                var winforms=automation.FromHandle(host.Handle).AsWindow();
                File.AppendAllText(Path.Combine(output,"steps.txt"),$"Original control {index}\n");
                WaitId(winforms,"ddlMarketDataSelector");
                if(index<2)WaitId(winforms,index==0?"lstFuturesContractIds":"lstFuturesOptionContractIds");else WaitName(winforms,"Time Period:");
                WaitEnabledId(winforms,"btnClose");
                Thread.Sleep(250);winforms.SetForeground();Thread.Sleep(150);
                winforms.Capture().Save(Path.Combine(output,$"winforms-{index}.png"));Dump(winforms,Path.Combine(output,$"winforms-{index}.json"));
                var manager=WaitName(rust,"Market Data Manager");
                rust.SetForeground();
                if(index>0) {
                var combo=manager.FindAllDescendants(cf=>cf.ByControlType(ControlType.ComboBox)).OrderBy(e=>e.BoundingRectangle.Top).First();
                if(combo.Patterns.Invoke.IsSupported)combo.Patterns.Invoke.Pattern.Invoke();else combo.Click();
                var choice=WaitName(rust,label);
                File.AppendAllText(Path.Combine(output,"steps.txt"),$"Rust choice {label}: {choice.ControlType}, invoke={choice.Patterns.Invoke.IsSupported}, select={choice.Patterns.SelectionItem.IsSupported}\n");
                if(choice.Patterns.Invoke.IsSupported)choice.Patterns.Invoke.Pattern.Invoke();else if(choice.Patterns.SelectionItem.IsSupported)choice.Patterns.SelectionItem.Pattern.Select();else choice.Click();
                WaitName(rust,"Add",true);
                }
                if(index<2)WaitName(rust,index==0?"Futures Contract Ids":"Futures Option Contract Ids");else WaitName(rust,"30 Yr");
                Thread.Sleep(250);rust.SetForeground();Thread.Sleep(150);
                rust.Capture().Save(Path.Combine(output,$"rust-{index}.png"));Dump(rust,Path.Combine(output,$"rust-{index}.json"));
                manager=WaitName(rust,"Market Data Manager");
                var originalAdd=WaitId(winforms,"btnAdd").BoundingRectangle;
                var nativeAdd=manager.FindFirstDescendant(cf=>cf.ByName("Add")).BoundingRectangle;
                Assert.InRange(Math.Abs(originalAdd.Width-nativeAdd.Width),0,3);
                Assert.InRange(Math.Abs(originalAdd.Height-nativeAdd.Height),0,3);
                Assert.True(manager.BoundingRectangle.Width > winforms.BoundingRectangle.Width); // Intentionally widened for all yield tenors.
                Assert.InRange(Math.Abs(winforms.BoundingRectangle.Height-manager.BoundingRectangle.Height),0,15);
                var nativeChange=manager.FindFirstDescendant(cf=>cf.ByName("Change"));
                if(nativeChange.IsEnabled) {
                    nativeChange.AsButton().Invoke();
                    if(index==2) {
                        Assert.Null(rust.FindFirstDescendant(cf=>cf.ByName("Change Yield Curve Rate")));
                        Assert.True(manager.FindAllDescendants(cf=>cf.ByControlType(ControlType.Edit)).Length >= 13);
                        rust.SetForeground();rust.Capture().Save(Path.Combine(output,"rust-yield-edit.png"));Dump(rust,Path.Combine(output,"rust-yield-edit.json"));
                    }
                    WaitName(rust,"Cancel",true).AsButton().Invoke();WaitName(rust,"Change",true);
                }
                var originalChange=WaitId(winforms,"btnChange");
                if(originalChange.IsEnabled && index<2) {
                    originalChange.AsButton().Invoke();var cancel=WaitId(winforms,"btnClose");
                    Assert.Contains("Cancel",cancel.Name);cancel.AsButton().Invoke();
                } else if(originalChange.IsEnabled) {
                    winforms.SetForeground();originalChange.Click();
                    var timer=Stopwatch.StartNew();Window? dialog=null;
                    while(dialog is null && timer.Elapsed<TimeSpan.FromSeconds(10)){var dialogHandle=host.FindFormHandle("Change Yield Curve Rate");if(dialogHandle!=IntPtr.Zero)dialog=automation.FromHandle(dialogHandle).AsWindow();else Thread.Sleep(100);}
                    Assert.NotNull(dialog);
                    dialog.SetForeground();dialog.Capture().Save(Path.Combine(output,"winforms-yield-edit.png"));Dump(dialog,Path.Combine(output,"winforms-yield-edit.json"));
                    WaitId(dialog,"btnCancel").AsButton().Invoke();
                }
                reports.Add(new{View=label,WinForms=winforms.BoundingRectangle.ToString(),Rust=manager.BoundingRectangle.ToString(),WinFormsAdd=originalAdd.ToString(),RustAdd=nativeAdd.ToString(),CancelWithoutSave=true});
                File.WriteAllText(Path.Combine(output,"parity-report.json"),JsonSerializer.Serialize(new{SelectorCatalog="In-memory baseline fixture because live catalog is empty; all other queries are live",Views=reports},new JsonSerializerOptions{WriteIndented=true}));
            }
        } finally {if(!process.HasExited){process.Kill();process.WaitForExit(10000);}}
    }
    public class SelectorCatalogProxy:DispatchProxy {
        public IReferenceDataService Target=null!;
        public int First;
        protected override object? Invoke(MethodInfo? method,object?[]? args) {
            if(method!.Name==nameof(IReferenceDataService.GetMarketDataDefinitionTypesAsync)) {
                IReadOnlyList<LookupTypeUiModel> rows=[new("MarketDataDefinitionType","FuturesContract",0,"Futures Contracts",DateTime.UnixEpoch,"ParityFixture"),new("MarketDataDefinitionType","FuturesOptionContract",1,"Futures Option Contracts",DateTime.UnixEpoch,"ParityFixture"),new("MarketDataDefinitionType","YieldCurveRates",2,"Yield Curve Rates",DateTime.UnixEpoch,"ParityFixture")];
                return ValueTask.FromResult(UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success(rows.OrderBy(row=>(row.OrderId-First+3)%3).ToArray()));
            }
            return method.Invoke(Target,args);
        }
    }
    sealed class DpiScope:IDisposable {
        readonly IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        public void Dispose()=>SetThreadDpiAwarenessContext(previous);
    }
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    static AutomationElement WaitId(Window window,string id)=>WaitNative(window,id,true,false);
    static AutomationElement WaitEnabledId(Window window,string id)=>WaitNative(window,id,true,true);
    static AutomationElement WaitName(Window window,string name,bool enabled=false)=>WaitNative(window,name,false,enabled);
    static AutomationElement WaitNative(Window window,string text,bool byId,bool enabled) {
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed<TimeSpan.FromSeconds(30)) {
            try {var found=byId?window.FindFirstDescendant(cf=>cf.ByAutomationId(text)):window.FindFirstDescendant(cf=>cf.ByName(text));
                if(found is not null && (!enabled || found.IsEnabled))return found;
            }catch(System.Runtime.InteropServices.COMException){}catch(FlaUI.Core.Exceptions.ElementNotAvailableException){}
            Thread.Sleep(100);
        }
        Dump(window,Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!,".artifacts/market-data-manager/flaui/failure-tree.json"));
        throw new TimeoutException("Missing "+text);
    }
    static IEnumerable<AutomationElement> Controls(AutomationElement parent,int depth=0) {
        if(depth>12)yield break;
        foreach(var child in parent.FindAllChildren()) {
            yield return child;
            var type=child.ControlType;
            if(type is ControlType.Table or ControlType.DataGrid or ControlType.List or ControlType.DataItem)continue;
            foreach(var nested in Controls(child,depth+1))yield return nested;
        }
    }
    static void Dump(Window window,string path) => File.WriteAllText(path,JsonSerializer.Serialize(new {
        Window=window.BoundingRectangle.ToString(),
        Controls=Controls(window).Select(e=>new {Type=e.ControlType.ToString(),Name=e.Properties.Name.ValueOrDefault,Id=e.Properties.AutomationId.ValueOrDefault,Bounds=e.BoundingRectangle.ToString(),Enabled=e.IsEnabled}).ToArray()
    },new JsonSerializerOptions{WriteIndented=true}));
}
public sealed class MarketDataParityFactAttribute:FactAttribute {
    public MarketDataParityFactAttribute(){if(Environment.GetEnvironmentVariable("IFM_MARKET_DATA_PARITY")!="1")Skip="Explicit live desktop parity verification only.";}
}

