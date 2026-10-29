using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

// Launch only the new Host, with isolated NATS subjects, a temporary configuration
// and fabricated storage values. StartAsync is never invoked; no provider I/O.
var broker=args.Length>1?args[1]:"nats://127.0.0.1:4222";
var binary=Path.GetFullPath(args[0]);var scope="nativeexe"+Guid.NewGuid().ToString("N");
var directory=Path.Combine(Path.GetTempPath(),"ifm-native-host-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
var settings=new{Storage=new{RedisConnection="127.0.0.1:1,abortConnect=false,connectTimeout=100,connectRetry=0",PostgresConnection="Host=127.0.0.1;Database=ifm_native_qualification;Username=fixture;Password=fixture"},ConnectionStrings=new{TradeTickQuoteDbConnection="Host=127.0.0.1;Database=ifm_native_qualification;Username=fixture;Password=fixture"},Telemetry=new{Logs=new{Enabled=false}},MarketDataHost=new{Scope=scope,NatsUrl=broker,DataSource="DatabentoLive",StdinShutdown=true}};
await File.WriteAllTextAsync(Path.Combine(directory,"appsettings.json"),JsonSerializer.Serialize(settings));
var start=new ProcessStartInfo(binary){WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
foreach(var argument in new[]{"--urls=http://127.0.0.1:"+port,"--MarketDataHost:ApiConfigurationDirectory="+directory,"--environment=Development"})start.ArgumentList.Add(argument);
start.Environment.Remove("DATABENTO_API_KEY");start.Environment.Remove("FMP_API_KEY");
using var process=Process.Start(start)??throw new InvalidOperationException("Native Host did not start.");
var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
try{
    using var http=new HttpClient();var ready=false;
    while(!ready){deadline.Token.ThrowIfCancellationRequested();if(process.HasExited)throw new InvalidDataException("Native Host exited during startup: "+await errors+await output);
        try{using var response=await http.GetAsync($"http://127.0.0.1:{port}/health/ready",deadline.Token);ready=response.IsSuccessStatusCode;}catch(HttpRequestException){}
        if(!ready)await Task.Delay(50,deadline.Token);
    }
    await using var manager=new NatsConnectionManager();await using var connection=new MarketDataHostConnection(manager,broker,scope);
    if(!await connection.PostAsync<bool>("ready",new{},deadline.Token))throw new InvalidDataException("Host NATS readiness failed.");
    var method=typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.GetBackendStatusAsync))!;
    var status=await connection.PostAsync<MarketDataBackendStatus>("api/GetBackendStatusAsync",new MarketDataApiCall(MarketDataApiClient.Signature(method),[[]]),deadline.Token);
    if(status.RecoveryOwnership!=MarketDataRecoveryOwnership.TokioDatasetOwner||!status.Ready||status.ValueDate is not null||status.Datasets.Count!=3||status.Datasets.Any(dataset=>dataset.Running||dataset.ValueDate is not null||dataset.Lifecycle!="Idle")||status.HostInstanceId==Guid.Empty)throw new InvalidDataException("Unstarted native Host did not report exactly three idle native controllers without a business date or live source.");
    method=typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.GetSubscriptionLeasesAsync))!;
    var leases=await connection.PostAsync<SubscriptionLeaseResult>("api/GetSubscriptionLeasesAsync",new MarketDataApiCall(MarketDataApiClient.Signature(method),[[],[]]),deadline.Token);
    if(leases.Code!=SubscriptionResultCode.Disabled)throw new InvalidDataException("Current additive lease contract changed.");
    process.Refresh();var modules=process.Modules.Cast<ProcessModule>().Select(x=>x.ModuleName).ToArray();
    if(!modules.Contains("ifm_market_data_native.dll",StringComparer.OrdinalIgnoreCase)||modules.Any(x=>x is "databento_feed_native.dll" or "ifm_treasury_native.dll"))throw new InvalidDataException("Host loaded a legacy native provider or omitted the new driver.");
    if(Native.Children((uint)process.Id,process.StartTime.ToUniversalTime()) is {Count:>0} children)throw new InvalidDataException("Native Host child processes: "+string.Join(", ",children));
    await process.StandardInput.WriteLineAsync("shutdown");await process.StandardInput.FlushAsync();await process.WaitForExitAsync(deadline.Token);
    if(process.ExitCode!=0)throw new InvalidDataException("Native Host shutdown failed: "+await errors+await output);
    Console.WriteLine("PASS native Host executable: real process startup, DLL readiness, isolated NATS API status, no invented ValueDate/subscriptions, exact disabled lease contract, no legacy provider DLL or dataset children, clean joined shutdown.");
}finally{
    if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
    await output;await errors;
    // Only the exact test-created temporary directory is removed.
    var resolved=Path.GetFullPath(directory);if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("ifm-native-host-",StringComparison.Ordinal))throw new InvalidDataException("Unsafe fixture cleanup target.");
    Directory.Delete(resolved,recursive:true);
}
static class Native{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Entry{public uint Size,Usage,Id;public nuint Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string File;}
    [DllImport("kernel32.dll",SetLastError=true)]static extern nint CreateToolhelp32Snapshot(uint flags,uint id);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32FirstW(nint snapshot,ref Entry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32NextW(nint snapshot,ref Entry entry);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(nint handle);
    internal static List<string> Children(uint parent,DateTime started){var snapshot=CreateToolhelp32Snapshot(2,0);if(snapshot==-1)throw new System.ComponentModel.Win32Exception();try{var entry=new Entry{Size=(uint)Marshal.SizeOf<Entry>(),File=""};if(!Process32FirstW(snapshot,ref entry))throw new System.ComponentModel.Win32Exception();var count=new List<string>();do{if(entry.Parent==parent)try{using var child=Process.GetProcessById((int)entry.Id);if(child.StartTime.ToUniversalTime()>=started){
    // Windows may allocate its own hidden console host for a console executable.
    // Only the actual System32 console helper is exempt; dataset/application
    // children remain a failure, irrespective of their process name.
    var systemConsole=Path.Combine(Environment.SystemDirectory,"conhost.exe");
    if(!string.Equals(child.MainModule?.FileName,systemConsole,StringComparison.OrdinalIgnoreCase))count.Add(entry.File+" ("+entry.Id+")");
}}catch(ArgumentException){}}while(Process32NextW(snapshot,ref entry));return count;}finally{CloseHandle(snapshot);}}
}
