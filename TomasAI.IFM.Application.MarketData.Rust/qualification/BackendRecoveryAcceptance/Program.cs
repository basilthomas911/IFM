using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

// Explicit live acceptance only, never an offline stage. Uses the already
// configured API ValueDate and exact observed ownership. It injects one guarded
// processing error and observes the real API supervisor/Host repair. No orders,
// trade intent or published policy are created or modified by this probe.
// --live-trade waits for full native readiness and repeats a controlled native
// recovery request twice. Its outer observer budget is test-only and does not
// change the dataset recovery deadline or emulate a blocked provider socket.
if(args.Length is <2 or >3||args[0] is not ("--live-development" or "--live-trade" or "--status" or "--inspect")||args[1]!="development")
    throw new ArgumentException("Explicit Development mode required: --live-development (background recovery), --live-trade (two loaded-trade recovery cycles), --status or --inspect.");
await using var manager=new NatsConnectionManager();
await using var connection=new MarketDataHostConnection(manager,args.Length==3?args[2]:"nats://127.0.0.1:4222",args[1]);
using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(args[0]=="--live-trade"?12:3));
Task<T> Call<T>(string name,params object[] values)
{
    var method=typeof(IMarketDataApi).GetMethods().Single(x=>x.Name==name);
    var payloads=values.Select(MarketDataHostConnection.Encode).Concat(new[]{Array.Empty<byte>()}).ToArray();
    return connection.PostAsync<T>("api/"+name,new MarketDataApiCall(MarketDataApiClient.Signature(method),payloads),deadline.Token);
}
var before=await Call<MarketDataBackendStatus>(nameof(IMarketDataApi.GetBackendStatusAsync));
if(args[0] is "--status" or "--inspect")
{
    var snapshot=await Call<MarketDataReadSnapshot>(nameof(IMarketDataApi.GetBackendReadSnapshotAsync));
    if(args[0]=="--inspect") {
        // Read-only copied data: no monitoring claim, refresh or lease is created.
        string Decode(byte[] bytes) {try{return MessagePack.MessagePackSerializer.ConvertToJson(bytes,MessagePack.MessagePackSerializerOptions.Standard.WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray));}catch(Exception error){return error.ToString();}}
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { snapshot.Status,snapshot.NativeOptionWorkload,
            Observations=(snapshot.OptionObservations??[]).Select(e=>new{e.Kind,e.GenerationId,e.PublicationSequence,Payload=Decode(e.Payload)}),
            Current=snapshot.CurrentValues.Select(e=>new{e.Kind,e.GenerationId,e.PublicationSequence,Payload=Decode(e.Payload)})}));
        return;
    }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Status=snapshot.Status,Workload=snapshot.NativeOptionWorkload,
        Publications=(snapshot.OptionChains??[]).Select(x=>new{x.LookupScope,x.Snapshot.ScopeId,x.Snapshot.GenerationId,x.Snapshot.EvaluatedAtUtc,x.Snapshot.ValidUntilUtc,Count=x.Snapshot.Instruments.Length})}));
    return;
}

if(args[0]=="--live-trade")
{
    if(!before.Ready||before.RecoveryOwnership!=MarketDataRecoveryOwnership.TokioDatasetOwner||before.EnabledOptionTrades is not {Length:>0})
        throw new InvalidOperationException("Explicit native loaded-trade acceptance requires an enabled trade on the native backend.");
    var tradeFutures=before.Datasets.Where(d=>d.Role=="Futures").ToArray();
    for(var round=1;round<=2;round++)
    {
        MarketDataReadSnapshot baseline;
        while(true) {
            baseline=await Call<MarketDataReadSnapshot>(nameof(IMarketDataApi.GetBackendReadSnapshotAsync));
            var observed=baseline.Status.Datasets.Single(d=>d.Role=="Options");var evidence=baseline.NativeOptionWorkload;
            if(observed.Healthy&&evidence is not null&&!evidence.ReplacementPending&&evidence.NativeResourceProofsComplete&&evidence.Scopes.Length>0&&evidence.Scopes.All(evidence.ReadyScopes.Contains))break;
            await Task.Delay(1000,deadline.Token);
        }
        var tradeOld=baseline.Status.Datasets.Single(d=>d.Role=="Options");
        var tradeOperation=Guid.NewGuid();var tradeStarted=Stopwatch.GetTimestamp();
        if(!await Call<bool>(nameof(IMarketDataApi.InjectDevelopmentOptionWorkerFaultAsync),new MarketDataDevelopmentFaultRequest(tradeOperation,tradeOld.WorkerInstanceId,tradeOld.GenerationId,before.ValueDate!.Value)))
            throw new InvalidDataException("Controlled native recovery fault was not accepted.");
        Console.WriteLine($"INJECT round={round}; tradeOperation={tradeOperation}; utc={DateTimeOffset.UtcNow:O}; previous_generation={tradeOld.GenerationId}");
        while(true)
        {
            var snapshot=await Call<MarketDataReadSnapshot>(nameof(IMarketDataApi.GetBackendReadSnapshotAsync));
            var status=snapshot.Status;var next=status.Datasets.Single(d=>d.Role=="Options");var proof=snapshot.NativeOptionWorkload;
            if(status.HostInstanceId!=before.HostInstanceId||next.ProcessId!=tradeOld.ProcessId||tradeFutures.Any(f=>!status.Datasets.Any(d=>d.Role==f.Role&&d.Dataset==f.Dataset&&d.GenerationId==f.GenerationId&&d.WorkerInstanceId==f.WorkerInstanceId&&d.Healthy&&d.Running)))
                throw new InvalidDataException("Options recovery disturbed Host or sibling tradeFutures ownership/health.");
            if(next.WorkerInstanceId!=tradeOld.WorkerInstanceId&&next.GenerationId!=tradeOld.GenerationId&&next.Healthy&&proof is not null&&proof.GenerationId==next.GenerationId&&!proof.ReplacementPending&&proof.NativeResourceProofsComplete&&proof.Scopes.Length>0&&proof.Scopes.All(proof.ReadyScopes.Contains))
            {
                Console.WriteLine($"PASS round={round}; tradeOperation={tradeOperation}; elapsed_ms={Stopwatch.GetElapsedTime(tradeStarted).TotalMilliseconds:F0}; generation={next.GenerationId}; scopes={proof.Scopes.Length}; full_native_proofs=true; same_host_pid=true; futures_unchanged=true; enabled_trades={string.Join(',',status.EnabledOptionTrades??[])}");
                break;
            }
            await Task.Delay(1000,deadline.Token);
        }
    }
    return;
}
if(!before.Ready||before.RecoveryOwnership!=MarketDataRecoveryOwnership.TokioDatasetOwner||before.ValueDate is null)
    throw new InvalidDataException("The selected Host is not an active native backend.");
if(before.EnabledOptionTrades is{Length:>0})throw new InvalidOperationException("This probe qualifies the no-loaded-trade/background-owner workload; live trade acceptance must be run separately.");
var old=before.Datasets.Single(x=>x.Role=="Options");
if(!old.Healthy||!old.Running)throw new InvalidDataException("The observed native option owner is not healthy before injection.");
var futures=before.Datasets.Where(x=>x.Role=="Futures").ToArray();var operation=Guid.NewGuid();
var injected=await Call<bool>(nameof(IMarketDataApi.InjectDevelopmentOptionWorkerFaultAsync),new MarketDataDevelopmentFaultRequest(operation,old.WorkerInstanceId,old.GenerationId,before.ValueDate.Value));
if(!injected)throw new InvalidDataException("Development injection was not accepted.");
var started=Stopwatch.GetTimestamp();
while(true)
{
    // Qualification-only observation; production market delivery remains event driven.
    var current=await Call<MarketDataBackendStatus>(nameof(IMarketDataApi.GetBackendStatusAsync));
    var next=current.Datasets.Single(x=>x.Role=="Options");
    if(current.HostInstanceId!=before.HostInstanceId)throw new InvalidDataException("Recovery replaced the Host instead of its options subtree.");
    if(next.Healthy&&next.Lifecycle=="Healthy"&&next.WorkerInstanceId!=old.WorkerInstanceId&&next.GenerationId!=old.GenerationId&&current.OptionProcessingFailure is null)
    {
        if(next.ProcessId!=old.ProcessId||futures.Any(x=>!current.Datasets.Any(y=>y.Role==x.Role&&y.Dataset==x.Dataset&&y.GenerationId==x.GenerationId&&y.WorkerInstanceId==x.WorkerInstanceId&&y.Healthy)))
            throw new InvalidDataException("Options recovery disturbed the actual Host/futures ownership.");
        Console.WriteLine($"PASS actual API-supervised native background-owner recovery; operation={operation}; elapsed_ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}; same Host/PID; changed options owner/generation; cleared processing failure; futures owners healthy and unchanged. Loaded-trade/UI terminal acceptance remains separate.");
        break;
    }
    await Task.Delay(200,deadline.Token);
}
