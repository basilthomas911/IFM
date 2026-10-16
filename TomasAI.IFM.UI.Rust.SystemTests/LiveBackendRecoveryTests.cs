using System.Diagnostics;
using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using Xunit;
public unsafe class LiveBackendRecoveryTests
{
 [Fact] public void Managed_client_recovers_after_owned_development_api_restart()
 {
  var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")??throw new InvalidOperationException("Set IFM_RUST_REPO_ROOT explicitly for this destructive-to-owned-process Development recovery test");
  var pidPath=Path.Combine(root,".artifacts/rust-ui/live-api.pid");
  var exe=Path.GetFullPath(Path.Combine(root,"TomasAI.IFM.Application.Api.Server/bin/Debug/net10.0/TomasAI.IFM.Application.Api.Server.exe"));
  using var backend=Process.GetProcessById(int.Parse(File.ReadAllText(pidPath).Trim()));
  Assert.Equal(exe,backend.MainModule!.FileName,ignoreCase:true);
  Ready();
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV4 table;Assert.Equal(0,get(4,(ApiV1*)&table,(uint)sizeof(ApiV4)));var api=table.Base;ulong handle;Assert.Equal(0,api.Create(&handle));
  var restarted=false;
  try {
   using var initial=Wait(api,handle,j=>j.GetProperty("roots").GetArrayLength()>0&&!j.GetProperty("status").GetString()!.Contains("Connecting"));
   backend.Kill();Assert.True(backend.WaitForExit(10000));
   Assert.Equal(0,api.Action(handle,2));
   using var offline=Wait(api,handle,j=>j.GetProperty("status").GetString()!.Contains("Unavailable")||j.GetProperty("status").GetString()!.Contains("Disconnected"));
   Restart(root);restarted=true;
   var deadline=DateTime.UtcNow.AddSeconds(120);bool ready=false;
   do {try {Ready();ready=true;}catch {Thread.Sleep(500);}}while(!ready&&DateTime.UtcNow<deadline);
   Assert.True(ready,"Restarted Development API did not become ready");
   Assert.Equal(0,api.Action(handle,2));
   using var recovered=Wait(api,handle,j=>j.GetProperty("roots").GetArrayLength()>0&&j.GetProperty("status").GetString()!.Contains("Live")&&!j.GetProperty("status").GetString()!.Contains("Unavailable"));
   Assert.True(recovered.RootElement.GetProperty("generation").GetInt64()>initial.RootElement.GetProperty("generation").GetInt64());
   File.WriteAllText(Path.Combine(root,".artifacts/rust-ui/tests/backend-recovery.json"),JsonSerializer.Serialize(new {Initial=initial.RootElement,Offline=offline.RootElement,Recovered=recovered.RootElement}));
  } finally {
   if(!restarted&&backend.HasExited)Restart(root);
   var destroyed=api.Destroy(handle);Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);Assert.Equal(0,destroyed);
  }
 }
 static void Restart(string root){var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/UI/Rust/Start-RecoveryDevelopmentApi.ps1"),"-RepositoryRoot",root})start.ArgumentList.Add(arg);var optionsPath=Path.Combine(root,".artifacts/rust-ui/live-api-options.json");if(File.Exists(optionsPath)){using var options=JsonDocument.Parse(File.ReadAllText(optionsPath));if(options.RootElement.GetProperty("WorkflowDelivery").GetBoolean())start.ArgumentList.Add("-WorkflowDelivery");}using var process=Process.Start(start)!;Assert.True(process.WaitForExit(10000));Assert.Equal(0,process.ExitCode);}
 static void Ready(){using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(2)};using var response=http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult();Assert.True(response.IsSuccessStatusCode);}
 static JsonDocument Wait(ApiV1 api,ulong handle,Func<JsonElement,bool> accepted){var deadline=DateTime.UtcNow.AddSeconds(45);string status="";do {int size;api.Copy(handle,0,null,0,&size);if(size>0){var bytes=new byte[size];fixed(byte* p=bytes){if(api.Copy(handle,0,p,size,&size)!=0)continue;}var doc=JsonDocument.Parse(MessagePack.MessagePackSerializer.ConvertToJson(bytes,BridgeWire.Options));if(doc.RootElement.TryGetProperty("error",out var error)){doc.Dispose();throw new Exception(error.ToString());}status=doc.RootElement.GetProperty("status").GetString()!;if(accepted(doc.RootElement))return doc;doc.Dispose();}Thread.Sleep(100);}while(DateTime.UtcNow<deadline);throw new TimeoutException("Client recovery publication timeout: "+status);}
}
