using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public unsafe class LiveContractEditorTests
{
 static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true};
 [Fact] public void Existing_contract_view_models_load_live_rows_and_option_pages_through_native_contract()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;Assert.Equal(0,get(2,(ApiV1*)&api,48));ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try
  {
   var futures=Run(api,handle,new(1,Guid.NewGuid(),"futures-contract","load",0,null,[]));
   var direct=Task.Run(ContractQueryDiagnostic.ReadAsync).GetAwaiter().GetResult();
   var diagnosticDir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(diagnosticDir);File.WriteAllText(Path.Combine(diagnosticDir,"contract-query-diagnostic.json"),JsonSerializer.Serialize(new{direct,editor=futures},Json));
   Assert.NotEmpty(futures.Result.Rows);Assert.NotEmpty(futures.Result.Choices["symbol"]);
   var options=Run(api,handle,new(1,Guid.NewGuid(),"option-contract","load",0,null,new(){["symbol"]="ES"}));
   Assert.NotEmpty(options.Result.Rows);Assert.True(options.Result.Rows.Length<=200);
   var first=options.Result.Rows.Select(x=>x.Id).ToArray();
   var more=options.Result.HasMore?Run(api,handle,new(1,Guid.NewGuid(),"option-contract","next",options.Generation,null,[])):options;
   Assert.Equal(more.Result.Rows.Length,more.Result.Rows.Select(x=>x.Id).Distinct().Count());Assert.All(first,id=>Assert.Contains(more.Result.Rows,x=>x.Id==id));
   var directory=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(directory);
   File.WriteAllText(Path.Combine(directory,"live-contract-qualification.json"),JsonSerializer.Serialize(new{futuresRows=futures.Result.Rows.Length,firstOptionRows=options.Result.Rows.Length,pagedOptionRows=more.Result.Rows.Length,outcomes=more.Outcomes},Json));
  }
  finally{Assert.Equal(0,api.Base.Destroy(handle));Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Published_provider_catalog_pages_are_snapshot_pinned_and_preserve_exact_facts()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;Assert.Equal(0,get(2,(ApiV1*)&api,48));ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try {
   var first=Run(api,handle,new(1,Guid.NewGuid(),"provider-definitions","load",0,null,new(){["dataset"]="GLBX.MDP3",["root"]="ES",["options"]="true"}));
   Assert.NotEmpty(first.Result.Rows);Assert.True(first.Result.Rows.Length<=100);Assert.True(first.Result.HasMore);
   Assert.All(first.Result.Rows,row=>{Assert.Equal(19,row.Fields.Count);Assert.NotEmpty(row.Fields["definitionDigest"]);Assert.NotEmpty(row.Fields["rawDefinitionReference"]);Assert.True(decimal.TryParse(row.Fields["strike"],System.Globalization.CultureInfo.InvariantCulture,out var strike)&&strike>0);});
   var second=Run(api,handle,new(1,Guid.NewGuid(),"provider-definitions","next",first.Generation,null,[]));
   Assert.Equal(first.Result.Context!["snapshotId"],second.Result.Context!["snapshotId"]);
   Assert.NotEmpty(second.Result.Rows);Assert.DoesNotContain(second.Result.Rows,row=>first.Result.Rows.Any(original=>original.Id==row.Id));
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"live-provider-browser-qualification.json"),JsonSerializer.Serialize(new{first.Result.Context,firstRows=first.Result.Rows.Length,nextRows=second.Result.Rows.Length,second.Outcomes},Json));
  }finally{Assert.Equal(0,api.Base.Destroy(handle));Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 [Fact] public void Cached_provider_mapping_previews_legacy_future_upgrade_without_writing()
 {
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;Assert.Equal(0,get(2,(ApiV1*)&api,48));ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try {
   var page=Run(api,handle,new(1,Guid.NewGuid(),"provider-definitions","load",0,null,new(){["dataset"]="GLBX.MDP3",["root"]="ES",["options"]="false"}));
   var selected=page.Result.Rows.Single(row=>row.Fields["rawSymbol"]=="ESZ6");
   var preview=Run(api,handle,new(1,Guid.NewGuid(),"provider-definitions","preview",page.Generation,selected.Id,new(){["existingContractId"]="ES20261218",["exchangeTimeZoneId"]="America/Chicago"}));
   Assert.Equal("Change",preview.Result.Context!["previewMode"]);Assert.Equal(selected.Id,preview.Result.Context["previewSelectedId"]);
   using var mapped=JsonDocument.Parse(preview.Result.Context["previewContract"]);var value=mapped.RootElement;
   Assert.Equal("ES20261218",value.GetProperty("contractId").GetString());Assert.Equal(1,value.GetProperty("schemaVersion").GetInt32());Assert.Equal(selected.Fields["definitionDigest"],value.GetProperty("definitionDigest").GetString());
   // Destroy cancels the preview; no publish intent is issued.
  }finally{Assert.Equal(0,api.Base.Destroy(handle));Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static ContractEditorPublication Run(ApiV2 api,ulong handle,ContractEditorIntent request)
 {
  var bytes=JsonSerializer.SerializeToUtf8Bytes(request,Json);fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));
  var deadline=DateTime.UtcNow.AddSeconds(45);
  do{var state=Read(api,handle);var outcome=state.Outcomes.SingleOrDefault(x=>x.RequestId==request.RequestId);if(!state.Busy&&outcome is not null&&outcome.State!="Pending"){Assert.True(outcome.State=="Completed",outcome.Message);return state;}Thread.Sleep(100);}while(DateTime.UtcNow<deadline);
  throw new TimeoutException("Contract load did not receive backend completion");
 }
 static ContractEditorPublication Read(ApiV2 api,ulong handle)
 {
  for(int attempt=0;attempt<3;attempt++){int n;api.Base.Copy(handle,5,null,0,&n);var bytes=new byte[n];fixed(byte* p=bytes){if(api.Base.Copy(handle,5,p,n,&n)!=0)continue;}return JsonSerializer.Deserialize<ContractEditorPublication>(bytes,Json)!;}throw new InvalidOperationException("Contract publication changed repeatedly");
 }
}

static class ContractQueryDiagnostic
{
 public static async Task<object> ReadAsync(){await using var scope=new RustClientServiceScope("nats://localhost:4222");await scope.StartAsync();var query=scope.Container.GetInstance<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi>();var result=await query.GetFuturesContractsAsync();return new{result.Success,result.ErrorCode,result.ErrorMessage,Rows=result.Value?.Length};}
}
