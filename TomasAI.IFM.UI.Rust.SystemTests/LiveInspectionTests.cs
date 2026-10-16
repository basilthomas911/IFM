using System.Runtime.CompilerServices;
using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public unsafe class LiveInspectionTests
{
 static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 [Fact] public void Development_api_supplies_observation_history_exact_workflow_and_risk_history()
 {
  using var http=new HttpClient();Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV4 api;get(4,(ApiV1*)&api,48);ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try{
   string? workflow=null;var deadline=DateTime.UtcNow.AddSeconds(30);
   do{var bytes=Copy(api,handle,0);if(bytes.Length>0){using var data=JsonDocument.Parse(MessagePack.MessagePackSerializer.ConvertToJson(bytes,BridgeWire.Options));if(data.RootElement.TryGetProperty("workflowId",out var id)&&id.GetString() is {Length:>0} value)workflow=value;}if(workflow is null)Thread.Sleep(100);}while(workflow is null&&DateTime.UtcNow<deadline);
   Assert.NotNull(workflow);
   var history=Run(api,handle,"observation","history",new(){["profile"]="ES.Standard",["horizon"]="Daily"});
   var exact=Run(api,handle,"observation","open",new(){["workflow"]=workflow});Assert.NotNull(exact.Result.Detail);Assert.Equal(workflow,exact.Result.Detail.WorkflowId);Assert.Equal(3,exact.Result.Detail.Roots.Length);
   var pair=Task.Run(LiveFundSelection.SelectAsync).GetAwaiter().GetResult();
   var risk=Run(api,handle,"risk","history",new(){["portfolio"]=pair.PortfolioId.ToString(),["fund"]=pair.FundId.ToString(),["date"]=DateTime.UtcNow.ToString("yyyy-MM-dd")});Assert.Equal("risk",risk.Result.View);
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"live-inspection-qualification.json"),JsonSerializer.Serialize(new{workflow,assessmentHistoryRows=history.Result.Rows.Length,exactRoots=exact.Result.Detail.Roots.Length,riskHistoryRows=risk.Result.Rows.Length,outcomes=risk.Outcomes},Json));
  }finally{Assert.Equal(0,api.Base.Destroy(handle));Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 internal static string ReadWorkflowIdentity()
 {
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV4 api;get(4,(ApiV1*)&api,48);ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try{var deadline=DateTime.UtcNow.AddSeconds(30);do{var bytes=Copy(api,handle,0);if(bytes.Length>0){using var data=JsonDocument.Parse(MessagePack.MessagePackSerializer.ConvertToJson(bytes,BridgeWire.Options));if(data.RootElement.TryGetProperty("workflowId",out var id)&&id.GetString() is {Length:>0} value)return value;}Thread.Sleep(100);}while(DateTime.UtcNow<deadline);throw new TimeoutException("The live API did not supply an existing workflow");}finally{Assert.Equal(0,api.Base.Destroy(handle));}
 }
 static byte[] Copy(ApiV4 api,ulong handle,uint kind){for(var attempt=0;attempt<3;attempt++){int n;api.Base.Copy(handle,kind,null,0,&n);var bytes=new byte[n];fixed(byte* p=bytes){var result=api.Base.Copy(handle,kind,p,n,&n);if(result==1)continue;Assert.Equal(0,result);}return bytes;}throw new InvalidOperationException("Publication grew beyond bounded retries");}
 static InspectionPublication Run(ApiV4 api,ulong handle,string view,string action,Dictionary<string,string> fields){var intent=new InspectionIntent(1,Guid.NewGuid(),view,action,fields);var bytes=BridgeWire.Serialize(intent);fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));var deadline=DateTime.UtcNow.AddSeconds(30);do{var publication=BridgeWire.Deserialize<InspectionPublication>(Copy(api,handle,4))!;var outcome=publication.Outcomes.FirstOrDefault(x=>x.RequestId==intent.RequestId);if(outcome is not null&&outcome.State!="Pending"){Assert.True(outcome.State=="Completed",outcome.Message);return publication;}Thread.Sleep(100);}while(DateTime.UtcNow<deadline);throw new TimeoutException("Inspection "+view+" "+action);}
}

static class LiveFundSelection
{
 public static async Task<(int PortfolioId,int FundId)> SelectAsync()
 {
  await using var scope=new RustClientServiceScope("nats://localhost:4222");await scope.StartAsync();
  foreach(var portfolioState in Enum.GetValues<TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioOperatingState>().Where(x=>x!=TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioOperatingState.Unknown)){
   var portfolios=await scope.Services.PortfolioQueries.GetPortfoliosAsync(portfolioState,100);Assert.True(portfolios.Success,portfolios.ErrorMessage);
   foreach(var portfolio in portfolios.Value!.Items){foreach(var fundState in Enum.GetValues<TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundOperatingState>().Where(x=>x!=TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundOperatingState.Unknown)){var funds=await scope.Services.PortfolioQueries.GetFundsAsync(portfolio.PortfolioId,fundState,100);Assert.True(funds.Success,funds.ErrorMessage);if(funds.Value!.Items.FirstOrDefault() is { } fund)return (portfolio.PortfolioId,fund.FundId);}}
  }
  throw new InvalidOperationException("No accessible Development fund is available for Risk history qualification");
 }
}
