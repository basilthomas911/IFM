using System.Text.Json;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.App;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Shares the read-only WinForms health projection, including clearing previous values on query failure.</summary>
public sealed class OperationsHealthSession : IDisposable
{
    readonly RustClientServiceScope? scope;
    readonly MarketDataOperationsHealthViewModel model;
    DateTime nextRefresh;
    public bool RefreshDue => DateTime.UtcNow>=nextRefresh;
    public OperationsHealthSession() {scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");model=new(scope.Container.GetInstance<IMarketDataOperationsHealthQueryService>());}
    public OperationsHealthSession(IMarketDataOperationsHealthQueryService service) {model=new(service);}
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {if(input.Intent!="load")throw new ArgumentException("Operations health is read-only");return await RefreshAsync();}
    public async Task<ContractEditorResult> RefreshAsync()
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await model.RefreshAsync(deadline.Token);nextRefresh=DateTime.UtcNow.AddSeconds(5);
        return new("operations-health",model.Stages.Select(x=>Row("stage:"+x.Stage,x,"stage")).Concat(model.Datasets.Select(x=>Row("dataset:"+x.Dataset,x,"dataset"))).ToArray(),[],Context:new(){["summary"]=model.Summary,["observation"]=model.Observation,["status"]=model.Status});
    }
    static ContractEditorRow Row<T>(string id,T value,string kind)
    {
        var json=JsonSerializer.SerializeToElement(value);var fields=json.EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.ValueKind==JsonValueKind.String?x.Value.GetString()??"":x.Value.ToString());fields["kind"]=kind;return new(id,fields);
    }
    public void Dispose(){model.DisposeAsync().AsTask().GetAwaiter().GetResult();scope?.DisposeAsync().AsTask().GetAwaiter().GetResult();}
}
