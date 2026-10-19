using System.Text.Json;
using FlaUI.UIA3;
public sealed class PortfolioModalProbeTests
{
    [PortfolioModalProbeFact]
    public void Inspect_existing_portfolio_modal_state()
    {
        using var automation=new UIA3Automation { TransactionTimeout=TimeSpan.FromSeconds(3),ConnectionTimeout=TimeSpan.FromSeconds(3) };
        var handle=new IntPtr(long.Parse(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_MODAL_HANDLE")!));
        var root=automation.FromHandle(handle);
        if(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_MODAL_CLOSE")=="1") {
            var child=root.FindFirstDescendant(cf=>cf.ByName("Create Portfolio"));
            Assert.NotNull(child);
            var close=child.FindFirstDescendant(cf=>cf.ByName("Close window"));
            Assert.NotNull(close);Assert.True(close.Patterns.Invoke.IsSupported);
            close.Patterns.Invoke.Pattern.Invoke();
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(root.FindFirstDescendant(cf=>cf.ByName("Create Portfolio")) is not null&&DateTime.UtcNow<deadline)Thread.Sleep(100);
        }
        var elements=root.FindAllDescendants();
        var report=elements.Select(e=>new {Name=e.Properties.Name.ValueOrDefault,Enabled=e.Properties.IsEnabled.ValueOrDefault,Type=e.Properties.ControlType.ValueOrDefault.ToString(),Bounds=e.Properties.BoundingRectangle.ValueOrDefault.ToString()}).ToArray();
        var output=Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!,".artifacts/portfolio-migration/flaui");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"live-modal-state.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Assert.Contains(report,e=>e.Name=="Portfolio Administration");
        if(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_MODAL_CLOSE")=="1") {
            Assert.DoesNotContain(report,e=>e.Name=="Create Portfolio");
            Assert.Contains(report,e=>e.Name=="Portfolio Actions"&&e.Enabled);
        }
    }
    sealed class PortfolioModalProbeFactAttribute:FactAttribute { public PortfolioModalProbeFactAttribute() { if(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_MODAL_PROBE")!="1")Skip="Explicit attachment to an existing Portfolio Administration window."; } }
}
