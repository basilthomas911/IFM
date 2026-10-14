using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public unsafe class LiveYieldCurveEditorTests
{
 static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 [Fact] public void Stored_curves_load_with_all_twelve_rates_and_period_selection()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
  Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;Assert.Equal(0,get(2,(ApiV1*)&api,48));ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try{
   var loaded=Run(api,handle,new(1,Guid.NewGuid(),"yield-curve","load",0,null,[]));
   Assert.Contains("2026",loaded.Result.Choices["timePeriod"]);
   var annual=Run(api,handle,new(1,Guid.NewGuid(),"yield-curve","range",loaded.Generation,null,new(){["timePeriod"]="2026"}));
   Assert.NotEmpty(annual.Result.Rows);
   string[] tenors=["oneMonth","twoMonth","threeMonth","sixMonth","oneYear","twoYear","threeYear","fiveYear","sevenYear","tenYear","twentyYear","thirtyYear"];
   Assert.All(annual.Result.Rows,row=>{Assert.Equal(13,row.Fields.Count);Assert.All(tenors,key=>Assert.True(double.TryParse(row.Fields[key],System.Globalization.CultureInfo.InvariantCulture,out var rate)&&double.IsFinite(rate)));});
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);
   File.WriteAllText(Path.Combine(dir,"live-yield-qualification.json"),JsonSerializer.Serialize(new{currentCount=loaded.Result.Rows.Length,annualCount=annual.Result.Rows.Length,tenors,annual.Result.Context,annual.Outcomes},Json));
  }finally{Assert.Equal(0,api.Base.Destroy(handle));Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static ContractEditorPublication Run(ApiV2 api,ulong handle,ContractEditorIntent intent){var bytes=JsonSerializer.SerializeToUtf8Bytes(intent,Json);fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));var deadline=DateTime.UtcNow.AddSeconds(45);do{int n;api.Base.Copy(handle,5,null,0,&n);var snapshot=new byte[n];fixed(byte* p=snapshot){if(api.Base.Copy(handle,5,p,n,&n)!=0)continue;}var state=JsonSerializer.Deserialize<ContractEditorPublication>(snapshot,Json)!;var outcome=state.Outcomes.SingleOrDefault(x=>x.RequestId==intent.RequestId);if(outcome is not null&&outcome.State!="Pending"){Assert.True(outcome.State=="Completed",outcome.Message);return state;}Thread.Sleep(100);}while(DateTime.UtcNow<deadline);throw new TimeoutException("Yield operation not completed");}
}
