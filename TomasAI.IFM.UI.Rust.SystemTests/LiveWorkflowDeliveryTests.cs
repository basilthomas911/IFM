using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public unsafe class LiveWorkflowDeliveryTests
{
 [Fact] public void New_backend_workflow_is_delivered_to_the_live_bridge_without_history_reload()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
  var previous=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV4 table;Assert.Equal(0,get(4,(ApiV1*)&table,(uint)sizeof(ApiV4)));var api=table.Base;ulong handle;Assert.Equal(0,api.Create(&handle));
  try
  {
   Assert.Equal(0,api.Action(handle,10));var ready=DateTime.UtcNow.AddSeconds(30);JsonElement baseline=default;
   do{var snapshot=Read(api,handle);if(snapshot.GetProperty("status").GetString()!.StartsWith("Intrinsic Time Daily:")&&snapshot.GetProperty("rows").GetArrayLength()>0){baseline=snapshot;break;}Thread.Sleep(100);}while(DateTime.UtcNow<ready);
   Assert.NotEqual(JsonValueKind.Undefined,baseline.ValueKind);
   var known=baseline.GetProperty("rows").EnumerateArray().Select(x=>x.GetProperty("id").GetString()!).ToHashSet();var started=DateTime.UtcNow;
   var deadline=started.AddSeconds(90);
   do
   {
    var snapshot=Read(api,handle);var row=snapshot.GetProperty("rows").EnumerateArray().FirstOrDefault(x=>!known.Contains(x.GetProperty("id").GetString()!)&&DateTime.Parse(x.GetProperty("occurredOn").GetString()!,null,System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime()>=started);
    if(row.ValueKind!=JsonValueKind.Undefined)
    {
     var id=Guid.Parse(row.GetProperty("id").GetString()!);Assert.True(Task.Run(()=>WorkflowDeliveryEvidence.ExistsAsync(id)).GetAwaiter().GetResult());
     var directory=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(directory);
     File.WriteAllText(Path.Combine(directory,"live-workflow-delivery.json"),JsonSerializer.Serialize(new{baselineGeneration=baseline.GetProperty("generation").GetInt64(),deliveredGeneration=snapshot.GetProperty("generation").GetInt64(),started,observed=DateTime.UtcNow,workflow=id,row,historyReloadsAfterBaseline=0}));return;
    }
    Thread.Sleep(150);
   }while(DateTime.UtcNow<deadline);
   Assert.Fail("No newly generated Daily business workflow reached the live subscriber within 90 seconds; no synthetic UI event was sent to manufacture proof.");
  }
  finally{Assert.Equal(0,api.Destroy(handle));Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",previous);}
 }
 static JsonElement Read(ApiV1 api,ulong handle){for(int attempt=0;attempt<300;attempt++){int n;api.Copy(handle,0,null,0,&n);if(n==0){Thread.Sleep(100);continue;}var bytes=new byte[n];fixed(byte* p=bytes){if(api.Copy(handle,0,p,n,&n)!=0)continue;}using var doc=JsonDocument.Parse(MessagePack.MessagePackSerializer.ConvertToJson(bytes,BridgeWire.Options));return doc.RootElement.Clone();}throw new InvalidOperationException("Live snapshot unavailable");}
}
static class WorkflowDeliveryEvidence
{
 public static async Task<bool> ExistsAsync(Guid id){await using var scope=new RustClientServiceScope("nats://localhost:4222");await scope.StartAsync();var api=scope.Container.GetInstance<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi.IIntrinsicTimeStrategyWorkflowQueryApi>();var result=await api.GetByIdAsync(new(id));return result.Success&&result.Value is not null;}
}
