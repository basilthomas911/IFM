using NSubstitute;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Views.Reference;

/// <summary>Read-only native desktop parity of the original Reference manager and all three reachable editors.</summary>
public sealed class ReferenceManagerParityTests
{
    /// <summary>Capture the real display documents used by the report grids for cross-language verification.</summary>
    [Fact]
    public void Display_generator_registers_closed_union_payloads_reached_through_the_base_type(){
        ReferenceDisplayUnion value=new ReferenceDisplayVariant("Exact payload");
        var bytes=TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Encode(value);
        var decoded=MessagePack.MessagePackSerializer.Deserialize<Dictionary<string,string>>(bytes);Assert.Equal("Exact payload",decoded["Description"]);
    }
    [Fact]
    public async Task Parameter_report_documents_are_present_in_the_live_bridge(){
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;var output=Path.Combine(root,".artifacts/reference-parity/documents");Directory.CreateDirectory(output);
        await using var session=new TomasAI.IFM.UI.Rust.Bridge.Net.ParameterSetSession();await session.ExecuteAsync(new(1,Guid.NewGuid(),"parameter-sets","load",0,null,[]));
        var regime=await session.ExecuteAsync(new(1,Guid.NewGuid(),"parameter-sets","component",0,null,new(){["component"]="strategy-workflow.regime-discovery"}));File.WriteAllText(Path.Combine(output,"regime-versions.json"),JsonSerializer.Serialize(regime.Rows));
        foreach(var (intent,key) in new[]{("startup-runs","startupRuns"),("startup-preview","startupPreview"),("legacy-parameters","legacyVersions")}){
            var result=await session.ExecuteAsync(new(1,Guid.NewGuid(),"parameter-sets",intent,0,null,[]));Assert.True(result.Documents.ContainsKey(key),key);File.WriteAllBytes(Path.Combine(output,key+".msgpack"),result.Documents[key]);
        }
    }
    [Fact]
    public void Reference_manager_preserves_original_selector_commands_and_three_editor_layouts()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT") ?? throw new InvalidOperationException("Set IFM_RUST_REPO_ROOT");
        var executable=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE") ?? throw new InvalidOperationException("Set IFM_RUST_UI_EXE");
        var output=Path.Combine(root,".artifacts/reference-parity/flaui");Directory.CreateDirectory(output);
        var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
            File.AppendAllText(Path.Combine(output,"progress.log"),"Creating baseline host\n");
            using var host=new DesktopViewTestHost(scope=>{
                var service=scope.Container.GetInstance<IReferenceDataService>();
                var form=new ReferenceForm(scope,service,scope.Services.ParameterSets);
                // The development selector catalogue currently omits LookupTypes. Supply only that catalogue entry in this test; editor data still uses the live service.
                var selectors=Substitute.For<IReferenceDataService>();
                selectors.GetReferenceDataDefinitionTypesAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([new("ReferenceDataDefinitionTypes","LookupTypes",0,"Lookup Types",DateTime.UtcNow,"Reference parity fixture")])));
                form.LoadViewModel(new ReferenceViewModel(selectors));
                return(form,()=>Task.CompletedTask);
            });
            using var automation=new UIA3Automation {TransactionTimeout=TimeSpan.FromSeconds(3),ConnectionTimeout=TimeSpan.FromSeconds(3)};
            var original=automation.FromHandle(host.Handle).AsWindow();
            File.AppendAllText(Path.Combine(output,"progress.log"),"Baseline host ready\n");
            var deadline=DateTime.UtcNow.AddSeconds(15);string[] sourceItems=[];
            do {sourceItems=host.Inspect(form=>form.Controls.Find("ddlReferenceDataSelector",true).OfType<System.Windows.Forms.ComboBox>().Single().Items.Cast<object>().Select(item=>item.ToString()!).ToArray());if(sourceItems.Length>=3)break;Thread.Sleep(100);}while(DateTime.UtcNow<deadline);
            File.WriteAllText(Path.Combine(output,"reference-selector.json"),JsonSerializer.Serialize(sourceItems));
            Assert.True(sourceItems.Length>=3,"WinForms Reference definitions do not contain all three editors: "+string.Join(",",sourceItems));
            var start=new ProcessStartInfo(executable){WorkingDirectory=Path.GetDirectoryName(executable)!};start.Environment["IFM_RUST_UI_MODE"]="live";
            using var app=Process.Start(start)!;
            try {
                var watch=Stopwatch.StartNew();IntPtr handle;
                while((handle=NativeWindows.Find(app.Id))==IntPtr.Zero&&watch.Elapsed<TimeSpan.FromSeconds(40))Thread.Sleep(100);
                Assert.NotEqual(IntPtr.Zero,handle);var rust=automation.FromHandle(handle).AsWindow();rust.SetForeground();
                OpenReference(rust,"Lookup Types");
                Wait(rust,e=>e.Properties.Name.ValueOrDefault=="Reference Data Manager","native Reference manager");
                foreach(var (name,marker) in new[]{("Lookup Types","Lookup Type Names"),("trade strategy families","Manage"),("parameter sets","Parameter Set")}) {
                    File.AppendAllText(Path.Combine(output,"progress.log"),"Selecting "+name+"\n");
                    host.Inspect(form=>{var selector=form.Controls.Find("ddlReferenceDataSelector",true).OfType<System.Windows.Forms.ComboBox>().Single();selector.SelectedIndex=selector.FindStringExact(name);return true;});
                    Wait(original,e=>e.Properties.Name.ValueOrDefault==marker,$"original {name} loaded");
                    File.AppendAllText(Path.Combine(output,"progress.log"),"Baseline loaded "+name+"\n");
                    var manager=Wait(rust,e=>e.Properties.Name.ValueOrDefault=="Reference Data Manager","manager");
                    var nativeSelector=Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.ComboBox&&e.IsEnabled,"native selector");
                    if(name!="Lookup Types") {
                        rust.SetForeground();
                        if(nativeSelector.Patterns.ExpandCollapse.IsSupported)nativeSelector.Patterns.ExpandCollapse.Pattern.Expand();else Click(nativeSelector);
                        Click(Wait(rust,e=>e.Properties.Name.ValueOrDefault==name&&e.Properties.ControlType.ValueOrDefault!=ControlType.ComboBox,$"selector {name}"));
                    }
                    Wait(rust,e=>e.Properties.Name.ValueOrDefault==marker,$"native {name} loaded");
                    File.AppendAllText(Path.Combine(output,"progress.log"),"Native loaded "+name+"\n");
                    foreach(var caption in new[]{"Add","Change","Remove","Import","Close"}) {
                        var native=Wait(manager,e=>e.Properties.Name.ValueOrDefault==caption&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,$"{name} {caption}");
                        var baseline=Wait(original,e=>(e.Properties.Name.ValueOrDefault??string.Empty).Replace("&","")==caption&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,$"original {caption}");
                        Assert.InRange(Math.Abs(native.BoundingRectangle.Width-baseline.BoundingRectangle.Width),0,10);
                    }
                    Assert.False(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Import"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"disabled Import").IsEnabled);
                    original.SetForeground();Thread.Sleep(300);host.Inspect(form=>{using var bitmap=new System.Drawing.Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(output,$"winforms-{name.Replace(' ','-')}.png"));return true;});
                    rust.SetForeground();rust.Capture().Save(Path.Combine(output,$"rust-{name.Replace(' ','-')}.png"));
                    File.WriteAllText(Path.Combine(output,$"rust-{name.Replace(' ','-')}.json"),JsonSerializer.Serialize(Snapshot(manager),new JsonSerializerOptions{WriteIndented=true}));
                    File.WriteAllText(Path.Combine(output,$"winforms-{name.Replace(' ','-')}.json"),JsonSerializer.Serialize(Snapshot(original),new JsonSerializerOptions{WriteIndented=true}));
                    if(name=="trade strategy families")CheckCatalogKinds(host,original,rust,output);
                    if(name=="parameter sets"){CheckParameterDialogs(host,automation,original,rust,output);CheckRegimeWorkingCopy(host,automation,original,rust,output);}
                }
                var closingManager=Wait(rust,e=>e.Properties.Name.ValueOrDefault=="Reference Data Manager","manager before close");
                Click(Wait(closingManager,e=>e.Properties.Name.ValueOrDefault=="Close"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"Reference Close"));
                OpenReference(rust,"Lookup Types");
            } finally {if(!app.HasExited){app.Kill(entireProcessTree:true);app.WaitForExit(10000);}}
        } finally {SetThreadDpiAwarenessContext(previous);}
    }
    /// <summary>Exercise read-only dialogs with the same live result as their original WinForms counterpart.</summary>
    static void CheckParameterDialogs(DesktopViewTestHost host,UIA3Automation automation,Window original,Window rust,string output,IEnumerable<(string Button,string Title)>? dialogs=null){
        foreach(var (button,title) in dialogs??new[]{("Payload","Parameter values"),("Schema","Parameter contract"),("Startup preview","Pending startup plan (preview only)"),("Recent startups","Recent parameter startup snapshots"),("Legacy versions","Legacy Regime Discovery versions (read only)"),("Operations","Committed parameter operations"),("Compare","Compare parameter versions")}){
            var source=Wait(original,e=>e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.Properties.Name.ValueOrDefault==button,"original "+button);
            var manager=Wait(rust,e=>e.Properties.Name.ValueOrDefault=="Reference Data Manager","manager");
            var native=Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.Properties.Name.ValueOrDefault==button,"native "+button);
            if(!source.IsEnabled||!native.IsEnabled){File.AppendAllText(Path.Combine(output,"dialog-availability.log"),button+": not available for the selected live component/version\n");continue;}
            File.AppendAllText(Path.Combine(output,"progress.log"),"Dialog "+title+"\n");
            rust.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);original.SetForeground();Thread.Sleep(350);source.Click();
            var deadline=DateTime.UtcNow.AddSeconds(30);IntPtr handle=IntPtr.Zero;while(DateTime.UtcNow<deadline&&(handle=host.FindFormHandle(title))==IntPtr.Zero)Thread.Sleep(100);
            Assert.NotEqual(IntPtr.Zero,handle);var baseline=automation.FromHandle(handle).AsWindow();
            if(button=="Recent startups")CheckStartupEvidence(host,automation,rust,baseline,output,true);
            baseline.Capture().Save(Path.Combine(output,"winforms-dialog-"+button+".png"));File.WriteAllText(Path.Combine(output,"winforms-dialog-"+button+".json"),JsonSerializer.Serialize(Snapshot(baseline)));baseline.Close();rust.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
            rust.SetForeground();Click(native);var dialog=Wait(rust,e=>e.Properties.Name.ValueOrDefault==title,"native dialog "+title);
            if(button=="Recent startups")CheckStartupEvidence(host,automation,rust,dialog.AsWindow(),output,false);
            dialog.Capture().Save(Path.Combine(output,"rust-dialog-"+button+".png"));File.WriteAllText(Path.Combine(output,"rust-dialog-"+button+".json"),JsonSerializer.Serialize(Snapshot(dialog)));
            Click(Wait(dialog,e=>e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.Properties.Name.ValueOrDefault=="Close window","dialog close"));
        }
    }
    /// <summary>Verify regime assignment and interval editing without saving, publishing, or assigning a version.</summary>
    static void CheckRegimeWorkingCopy(DesktopViewTestHost host,UIA3Automation automation,Window original,Window rust,string output){
        var area=Wait(original,e=>e.Properties.Name.ValueOrDefault=="Strategy Workflow"&&e.Properties.ControlType.ValueOrDefault==ControlType.ListItem,"original workflow area");area.Patterns.SelectionItem.Pattern.Select();
        var manager=Wait(rust,e=>e.Properties.Name.ValueOrDefault=="Reference Data Manager","manager");Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Strategy Workflow"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"workflow area"));
        Wait(original,e=>e.Properties.Name.ValueOrDefault=="Regime Discovery"&&e.Properties.ControlType.ValueOrDefault==ControlType.ListItem,"original regime component").Patterns.SelectionItem.Pattern.Select();
        Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Regime Discovery"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"regime component"));
        Thread.Sleep(500);
        var published=original.FindAllDescendants().FirstOrDefault(e=>e.Properties.ControlType.ValueOrDefault==ControlType.ListItem&&(e.Properties.Name.ValueOrDefault??"").EndsWith("(Published)"));
        if(published is not null){var publishedName=published.Properties.Name.ValueOrDefault!;published.Patterns.SelectionItem.Pattern.Select();
            Click(Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.Properties.Name.ValueOrDefault==publishedName,"published regime version in Rust"));
            Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.Properties.Name.ValueOrDefault=="Assignment"&&e.IsEnabled,"regime assignment eligibility");
            CheckParameterDialogs(host,automation,original,rust,output,[("Assignment","Workflow parameter assignment")]);
        }else File.AppendAllText(Path.Combine(output,"dialog-availability.log"),"Assignment: live qualification unavailable; no published Regime Discovery version. No configuration was published by this test.\n");
        rust.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);original.SetForeground();
        var add=Wait(original,e=>(e.Properties.Name.ValueOrDefault??"").Replace("&","")=="Add"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.IsEnabled,"original add");Click(add);
        Wait(original,e=>(e.Properties.Name.ValueOrDefault??"").Replace("&","")=="Save"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"original working copy");
        rust.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);rust.SetForeground();Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Add"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.IsEnabled,"native add"));
        Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Save"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"native working copy");
        CheckParameterDialogs(host,automation,original,rust,output,[("Intervals","Daily signal intervals")]);
        // Cancel the working copies; no persistent mutation is sent by this test.
        rust.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);original.SetForeground();Click(Wait(original,e=>(e.Properties.Name.ValueOrDefault??"").Replace("&","")=="Cancel"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"original cancel"));
        rust.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);rust.SetForeground();Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Cancel"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"native cancel"));
        Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Add"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.IsEnabled,"restored native command rail");
    }
    /// <summary>Exercise startup evidence and its monitored-signal child through the original row double-click route.</summary>
    static void CheckStartupEvidence(DesktopViewTestHost host,UIA3Automation automation,Window rust,Window runs,string output,bool original){
        var cell=Wait(runs,e=>original ? e.Properties.ControlType.ValueOrDefault==ControlType.DataItem&&(e.Properties.Name.ValueOrDefault??"").StartsWith("RunId Row 1,") : Guid.TryParse(e.Properties.Name.ValueOrDefault,out _),"startup run row");
        cell.DoubleClick();
        AutomationElement FindDialog(string title){if(!original)return Wait(rust,e=>e.Properties.Name.ValueOrDefault==title,title);var deadline=DateTime.UtcNow.AddSeconds(30);IntPtr handle=IntPtr.Zero;while(DateTime.UtcNow<deadline&&(handle=host.FindFormHandle(title))==IntPtr.Zero)Thread.Sleep(100);Assert.NotEqual(IntPtr.Zero,handle);return automation.FromHandle(handle);}
        var evidence=FindDialog("Startup preparation evidence");
        var prefix=original?"winforms":"rust";evidence.Capture().Save(Path.Combine(output,prefix+"-dialog-Startup-evidence.png"));File.WriteAllText(Path.Combine(output,prefix+"-dialog-Startup-evidence.json"),JsonSerializer.Serialize(Snapshot(evidence)));
        var refresh=Wait(evidence,e=>e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.Properties.Name.ValueOrDefault=="Refresh monitored signals","monitor refresh");if(original)refresh.Click();else Click(refresh);
        var monitor=FindDialog("Monitored signal availability");monitor.Capture().Save(Path.Combine(output,prefix+"-dialog-Monitored-signals.png"));File.WriteAllText(Path.Combine(output,prefix+"-dialog-Monitored-signals.json"),JsonSerializer.Serialize(Snapshot(monitor)));
        if(original){monitor.AsWindow().Close();evidence.AsWindow().Close();}else{Click(Wait(monitor,e=>e.Properties.Name.ValueOrDefault=="Close window","close monitor"));evidence=FindDialog("Startup preparation evidence");Click(Wait(evidence,e=>e.Properties.Name.ValueOrDefault=="Close window","close evidence"));}
    }
    /// <summary>Verify every original catalogue kind and local draft tabs, cancelling without persistent writes.</summary>
    static void CheckCatalogKinds(DesktopViewTestHost host,Window original,Window rust,string output){
        var manager=Wait(rust,e=>e.Properties.Name.ValueOrDefault=="Reference Data Manager","manager");
        foreach(var kind in new[]{"Family","Strategy","Structure","Variant","ParameterSchema","ParameterSet","Deployment"}){
            File.AppendAllText(Path.Combine(output,"progress.log"),"Catalog kind "+kind+"\n");
            host.Inspect(form=>{System.Windows.Forms.Control[] Descendants(System.Windows.Forms.Control control)=>control.Controls.Cast<System.Windows.Forms.Control>().SelectMany(child=>new[]{child}.Concat(Descendants(child))).ToArray();var combo=Descendants(form).OfType<System.Windows.Forms.ComboBox>().Single(control=>control.AccessibleName=="Catalog section");combo.SelectedItem=combo.Items.Cast<object>().Single(item=>item.ToString()==kind);return true;});
            rust.SetForeground();var selector=Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.ComboBox&&e.Properties.Name.ValueOrDefault=="Catalog section"&&e.IsEnabled,"catalog section");if(kind!="Family"){if(selector.Patterns.ExpandCollapse.IsSupported)selector.Patterns.ExpandCollapse.Pattern.Expand();else Click(selector);var bounds=selector.BoundingRectangle;Click(Wait(rust,e=>e.Properties.Name.ValueOrDefault==kind&&e.Properties.ControlType.ValueOrDefault!=ControlType.ComboBox&&e.BoundingRectangle.Left>=bounds.Left-5&&e.BoundingRectangle.Top>=bounds.Bottom-2,"catalog kind "+kind));}
            var sourceAdd=Wait(original,e=>(e.Properties.Name.ValueOrDefault??"").Replace("&","")=="Add"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.IsEnabled,"original add "+kind);Click(sourceAdd);
            Wait(original,e=>(e.Properties.Name.ValueOrDefault??"").Replace("&","")=="Save"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"original draft "+kind);
            Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Add"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.IsEnabled,"native add "+kind));Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Save"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"native draft "+kind);
            foreach(var caption in new[]{"Code","Name","Description","Version"})Wait(manager,e=>e.Properties.Name.ValueOrDefault==caption,"catalog field "+caption);
            var tabs=kind switch{"Strategy"=>new[]{"Families","Structures"},"Structure"=>new[]{"Expiry groups","Legs"},"Variant"=>new[]{"Variant","Variant legs"},"ParameterSchema"=>new[]{"Parameter fields","Capabilities"},"ParameterSet"=>new[]{"Capabilities","Parameter values"},"Deployment"=>new[]{"Variants","Products","Pipeline profiles","Parameter sets","Legacy mappings"},_=>Array.Empty<string>()};
            foreach(var tab in tabs)Wait(manager,e=>e.Properties.Name.ValueOrDefault==tab&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"catalog tab "+tab);
            host.Inspect(form=>{using var bitmap=new System.Drawing.Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(output,"winforms-catalog-"+kind+".png"));return true;});
            File.WriteAllText(Path.Combine(output,"rust-catalog-"+kind+".json"),JsonSerializer.Serialize(Snapshot(manager)));rust.Capture().Save(Path.Combine(output,"rust-catalog-"+kind+".png"));
            Click(Wait(original,e=>(e.Properties.Name.ValueOrDefault??"").Replace("&","")=="Cancel"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"original cancel "+kind));Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Cancel"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"native cancel "+kind));
        }
    }
    /// <summary>Open Reference directly, then choose the editor inside its manager as WinForms does.</summary>
    internal static void OpenReference(AutomationElement root,string selection)
    {
        var referenceButton=Wait(root,e=>e.Properties.Name.ValueOrDefault=="Reference","Reference button");
        Click(referenceButton);
        var manager=Wait(root,e=>e.Properties.Name.ValueOrDefault=="Reference Data Manager","direct Reference manager");
        Assert.DoesNotContain(referenceButton.FindAllDescendants(),e=>e.Properties.ControlType.ValueOrDefault==ControlType.MenuItem);
        Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Lookup Type Names","default lookup editor");
        if(selection=="Lookup Types")return;
        var selector=Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.ComboBox&&e.IsEnabled,"Reference Selection dropdown");
        if(selector.Patterns.ExpandCollapse.IsSupported)selector.Patterns.ExpandCollapse.Pattern.Expand();else Click(selector);
        Click(Wait(root,e=>e.Properties.Name.ValueOrDefault==selection&&e.Properties.ControlType.ValueOrDefault!=ControlType.ComboBox,"Reference selection "+selection));
    }
    static object[] Snapshot(AutomationElement root)=>root.FindAllDescendants().Select(element=>{
        try{return (object)new{Name=element.Properties.Name.ValueOrDefault,Type=element.ControlType.ToString(),element.IsEnabled,Bounds=element.BoundingRectangle.ToString()};}
        catch(Exception error) when(error is COMException or FlaUI.Core.Exceptions.PropertyNotSupportedException){return new{Name="<stale>"};}
    }).ToArray();
    static void Click(AutomationElement element){if(element.Patterns.Invoke.IsSupported)element.Patterns.Invoke.Pattern.Invoke();else element.Click();Thread.Sleep(250);}
    static AutomationElement Wait(AutomationElement root,Func<AutomationElement,bool> predicate,string description){
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while(DateTime.UtcNow<deadline){try{foreach(var element in root.FindAllDescendants()){try{if(predicate(element))return element;}catch(Exception error) when(error is COMException or FlaUI.Core.Exceptions.PropertyNotSupportedException){}}}catch(Exception error) when(error is COMException or FlaUI.Core.Exceptions.PropertyNotSupportedException){}Thread.Sleep(100);}
        try {var output=Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!,".artifacts/reference-parity/flaui");root.Capture().Save(Path.Combine(output,"failure.png"));File.WriteAllText(Path.Combine(output,"failure-tree.json"),JsonSerializer.Serialize(Snapshot(root),new JsonSerializerOptions{WriteIndented=true}));}catch{}
        throw new TimeoutException("Missing "+description);
    }
    [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}

[MessagePack.Union(0,typeof(ReferenceDisplayVariant))]
public abstract record ReferenceDisplayUnion;
public sealed record ReferenceDisplayVariant(string Description):ReferenceDisplayUnion;
