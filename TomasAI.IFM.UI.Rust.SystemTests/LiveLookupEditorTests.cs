using System.Text.Json;
using System.Runtime.CompilerServices;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Models.Reference;
public unsafe class LiveLookupEditorTests
{
 static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 [Fact] public void Existing_view_model_creates_changes_and_removes_only_its_disposable_lookup()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;Assert.Equal(0,get(2,(ApiV1*)&api,48));ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  var name="RustUiQualification-"+Guid.NewGuid().ToString("N");var row=new LookupTypeUiModel(name,"TEST",0,"Initial disposable definition",DateTime.UtcNow,"UI qualification");bool created=false;
  try
  {
   var loaded=Run(api,handle,"load",0,null);Assert.Equal("Completed",loaded.Outcomes.Last().State);
   var added=Run(api,handle,"add",loaded.Generation,row);created=added.Rows.Any(x=>x.LookupTypeName==name);Assert.True(created);Assert.Equal("Completed",added.Outcomes.Last().State);
   var changed=Run(api,handle,"change",added.Generation,row with{Description="Verified changed definition"});Assert.Equal("Verified changed definition",changed.Rows.Single(x=>x.LookupTypeName==name).Description);
   var removed=Run(api,handle,"remove",changed.Generation,row);Assert.DoesNotContain(removed.Rows,x=>x.LookupTypeName==name);Assert.Equal("Completed",removed.Outcomes.Last().State);created=false;
   var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"live-lookup-qualification.json"),JsonSerializer.Serialize(new{category=name,added=added.Outcomes,changed=changed.Outcomes,removed=removed.Outcomes,remainingTestRows=removed.Rows.Count(x=>x.LookupTypeName==name)},Json));
  }
  finally
  {
   try{if(created){var state=Read(api,handle);if(!state.Busy)Run(api,handle,"remove",state.Generation,row);}}
   finally{Assert.Equal(0,api.Base.Destroy(handle));Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
  }
 }
 static EditorPublication Run(ApiV2 api,ulong handle,string action,long generation,LookupTypeUiModel? row)
 {
  var request=new EditorIntent(1,Guid.NewGuid(),"lookup",action,generation,row);var bytes=JsonSerializer.SerializeToUtf8Bytes(request,Json);
  fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));
  var deadline=DateTime.UtcNow.AddSeconds(45);
  do{var state=Read(api,handle);var outcome=state.Outcomes.FirstOrDefault(x=>x.RequestId==request.RequestId);if(outcome is not null&&outcome.State!="Pending"){Assert.Equal("Completed",outcome.State);return state;}Thread.Sleep(100);}while(DateTime.UtcNow<deadline);
  throw new TimeoutException("Lookup "+action+" did not receive backend completion");
 }
 static EditorPublication Read(ApiV2 api,ulong handle){int n;api.Base.Copy(handle,3,null,0,&n);var bytes=new byte[n];fixed(byte* p=bytes)Assert.Equal(0,api.Base.Copy(handle,3,p,n,&n));return JsonSerializer.Deserialize<EditorPublication>(bytes,Json)!;}
}
