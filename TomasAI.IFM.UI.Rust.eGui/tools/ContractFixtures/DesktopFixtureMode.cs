using System.Net;
using System.Text;
using System.Text.Json;
/// <summary>Opt-in desktop-only infrastructure. Strict protocol gates retain
/// their frozen API-clock assertions. Desktop evidence keeps actual request
/// metadata; it never rewrites a financial payload or canonical hash.</summary>
internal static class DesktopFixtureMode{
 public static bool Enabled{get;set;}
 // Health freshness is operational, so this isolated scenario asks the fake
 // API for a moving clock. Ordinary scenarios keep the frozen backtest clock.
 public static bool OperationalClock=>Enabled&&Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_OPERATIONAL_CLOCK")=="1";
 public static bool Clock(DateTime value,DateTime expected)=>value==expected||Enabled&&value.Kind==DateTimeKind.Utc&&Math.Abs((value-DateTime.UtcNow).TotalMinutes)<5;
 public static bool Clock(DateTimeOffset value,DateTimeOffset expected)=>value==expected||Enabled&&Math.Abs((value-DateTimeOffset.UtcNow).TotalMinutes)<5;
 public static bool HostDeadline(DateTime value)=>!Enabled?value==StrategyFixtures.At.AddSeconds(2):value.Kind==DateTimeKind.Utc&&((value>StrategyFixtures.At&&value<=StrategyFixtures.At.AddMinutes(2))||(value>DateTime.UtcNow.AddSeconds(-2)&&value<=DateTime.UtcNow.AddMinutes(2).AddSeconds(2)));
 public static async Task ServeHttp(string fixtures,CancellationToken token){
  var raw=Environment.GetEnvironmentVariable("IFM_STANDALONE_DESKTOP_HTTP_URL");
  if(!Uri.TryCreate(raw,UriKind.Absolute,out var address)||!address.IsLoopback||address.Port==22543)throw new InvalidOperationException("Desktop fixtures require a separate loopback HTTP port");
  var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,address.Port);listener.Start();
  try{while(!token.IsCancellationRequested){using var client=await listener.AcceptTcpClientAsync(token);await using var stream=client.GetStream();using var requestTimeout=CancellationTokenSource.CreateLinkedTokenSource(token);requestTimeout.CancelAfter(TimeSpan.FromSeconds(10));
   var buffer=new byte[16384];var count=0;while(count<buffer.Length){var read=await stream.ReadAsync(buffer.AsMemory(count),requestTimeout.Token);if(read==0)break;count+=read;if(Encoding.ASCII.GetString(buffer,0,count).Contains("\r\n\r\n",StringComparison.Ordinal))break;}
   var line=Encoding.ASCII.GetString(buffer,0,count).Split('\n')[0].Split(' ');var path=line.Length>=2?line[1].Split('?')[0]:"";string body;
   if(path=="/api/actor-health")body=File.ReadAllText(Path.Combine(fixtures,"actor-health-http.json"));
   else if(path.EndsWith("live-health",StringComparison.Ordinal))body=JsonSerializer.Serialize(new{observedUtc=StrategyFixtures.At,status="Healthy",checks=new[]{new{required=true,status="Healthy",reason="Synthetic desktop fixture"}}});
   else body=JsonSerializer.Serialize(new TomasAI.IFM.UI.Net.Models.MarketData.MarketDataOperationsHealthSnapshot{Revision=7,ObservedOnUtc=OperationalClock?DateTime.UtcNow:StrategyFixtures.At,OverallStatus="Yellow",SessionState="Active",ValueDate=new(2026,10,26),Stages=[new(){Stage="OptionFeed",Status="Yellow",ReasonCode="Fixture",Reason="Synthetic acceptance data",Required=true,Received=20,Completed=19,Pending=1,Capacity=32}],Datasets=[new(){Dataset="GLBX.MDP3.Options",Status="Orange",Reason="Synthetic recovery in progress",SessionState="Active",ValueDate=new(2026,10,26),ProcessId=12345,WorkerInstanceId=Guid.Parse("11111111-2222-3333-4444-555555555555"),GenerationId=Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),StartedOnUtc=StrategyFixtures.At.AddMinutes(-5),LastObservedUtc=StrategyFixtures.At,CooperativeAttempts=2,ProcessReplacementCount=1,Running=true,Healthy=false,RecordsProduced=20,RecordsConsumed=19,RingUsed=1,RingCapacity=32,ChannelBatchCount=1,ChannelBatchCapacity=8,RecordsStarted=19,RecordsCompleted=18}]},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
   var bytes=Encoding.UTF8.GetBytes(body);var header=Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");await stream.WriteAsync(header,token);await stream.WriteAsync(bytes,token);
  }}catch(OperationCanceledException)when(token.IsCancellationRequested){}finally{listener.Stop();}
 }
}
