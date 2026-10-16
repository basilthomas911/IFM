using System.Text.Json;
using System.Runtime.CompilerServices;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Models.Reference;
public class EditorActionTests
{
 static LookupTypeUiModel Row()=>new("Rust test","A",1,"Description",DateTime.UtcNow,"Test");
 [Fact] public async Task Duplicate_intents_share_one_completion_and_conflicting_reuse_is_rejected()
 {
  var done=new TaskCompletionSource<LookupTypeUiModel[]>();int executions=0;
  var gateway=new EditorActionGateway(_=>{Interlocked.Increment(ref executions);return done.Task;});
  var request=new EditorIntent(1,Guid.NewGuid(),"lookup","load",0,null);
  Assert.Equal(0,gateway.Submit(request));Assert.Equal(0,gateway.Submit(request));Assert.Equal(-1,gateway.Submit(request with{Intent="add"}));
  Assert.True(gateway.Snapshot().Busy);Assert.Equal("Pending",gateway.Snapshot().Outcomes.Single().State);
  done.SetResult([Row()]);await gateway.DrainAsync();Assert.Equal(1,executions);Assert.Equal("Completed",gateway.Snapshot().Outcomes.Single().State);Assert.False(gateway.Snapshot().Busy);
  Assert.Equal(0,gateway.Submit(request));Assert.Equal(1,executions);
 }
 [Fact] public async Task Stale_or_invalid_edits_never_execute_and_failures_are_acknowledged()
 {
  var gateway=new EditorActionGateway(_=>throw new InvalidOperationException("Backend rejected input"));
  Assert.Equal(-5,gateway.Submit(new(1,Guid.NewGuid(),"lookup","add",0,Row())));
  Assert.Equal(-1,gateway.Submit(new(1,Guid.NewGuid(),"unknown","load",0,null)));
  Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"lookup","load",0,null)));await gateway.DrainAsync();
  Assert.Equal("Failed",gateway.Snapshot().Outcomes.Single().State);Assert.Contains("Backend rejected",gateway.Snapshot().Status);
 }
 [Fact] public unsafe void V4_abi_validates_binary_editor_payloads()
 {
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV4 api;
  Assert.Equal(48,sizeof(ApiV4));Assert.Equal(-1,get(4,(ApiV1*)&api,40));Assert.Equal(0,get(4,(ApiV1*)&api,48));
  Assert.Equal(4u,api.Base.Version);ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try {Assert.Equal(-1,api.Submit(handle,null,4));var bytes=System.Text.Encoding.UTF8.GetBytes("{bad");fixed(byte* p=bytes)Assert.Equal(-1,api.Submit(handle,p,bytes.Length));int n;Assert.Equal(1,api.Base.Copy(handle,3,null,0,&n));Assert.True(n>0);}
  finally {Assert.Equal(0,api.Base.Destroy(handle));}
 }
}
