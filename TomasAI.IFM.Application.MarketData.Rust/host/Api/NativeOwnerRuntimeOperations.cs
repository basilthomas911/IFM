using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Thin Host control/observation of native dataset authority. No
/// managed recovery timer, callback registry, epoch, gate or lease is retained.</summary>
public sealed class NativeOwnerRuntimeOperations(NativeDatasetBackend backend,
    NativeOwnerTradeRecovery trades, IServiceProvider services)
{
    bool Ready=>services.GetRequiredService<NativeMarketDataNatsService>().IsReady;
    MarketDataBackendStatus Business(MarketDataBackendStatus status)=>status with{
        EnabledOptionTrades=status.ValueDate is{} date?trades.EnabledTrades(date):[]};
    /// <summary>Copies native status and detached enabled business identities.</summary>
    public Task<MarketDataBackendStatus> GetBackendStatusAsync(CancellationToken token=default)
    {token.ThrowIfCancellationRequested();return Task.FromResult(Business(backend.Runtime.ReadDatasetOwnerBackendStatus(Ready)));}
    /// <summary>Copies the complete native fenced snapshot without managed mirror caches.</summary>
    public Task<MarketDataReadSnapshot> GetBackendReadSnapshotAsync(CancellationToken token=default)
    {token.ThrowIfCancellationRequested();var snapshot=backend.Runtime.ReadDatasetOwnerSnapshot(Ready);return Task.FromResult(snapshot with{Status=Business(snapshot.Status)});}
    /// <summary>Native code accepts and owns the original two-minute episode.</summary>
    public Task<MarketDataRecoveryResult> RecoverMarketDataAsync(MarketDataRecoveryRequest request,CancellationToken token=default)
        =>backend.Runtime.RecoverDatasetOwnerAsync(request,token);
    /// <summary>Accepts independent native resets concurrently. Each dataset
    /// preserves its own original deadline; no C# timeout can truncate/renew it.</summary>
    public async Task ResetMarketDataAsync(DateOnly date,Guid operation,CancellationToken token=default)
    {
        var status=await GetBackendStatusAsync(token).ConfigureAwait(false);
        if(status.ValueDate!=date||operation==Guid.Empty)throw new InvalidOperationException("Invalid native reset identity/ValueDate.");
        var results=await Task.WhenAll(status.Datasets.Where(dataset=>dataset.Lifecycle!="Idle").Select(dataset=>RecoverMarketDataAsync(
            new(operation,dataset.Role,dataset.Dataset,dataset.WorkerInstanceId,dataset.GenerationId,date,"Requested native reset"),token))).ConfigureAwait(false);
        if(results.Any(result=>!result.Recovered))throw new InvalidOperationException(string.Join("; ",results.Where(result=>!result.Recovered).Select(result=>result.Error)));
    }
    /// <summary>Repairs options by replacing their isolated native dataset.
    /// Futures resources remain untouched. Stale episode identities cannot reset it.</summary>
    public async Task<OptionObservationRepairResult> RepairOptionObservationsAsync(OptionObservationRepairRequest request,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status=await GetBackendStatusAsync(token).ConfigureAwait(false);
        var options=status.Datasets.Single(dataset=>dataset.Role=="Options"&&dataset.Dataset=="GLBX.MDP3");
        if(request.ValueDate!=status.ValueDate||request.GenerationId!=options.GenerationId||request.OperationId==Guid.Empty)throw new InvalidOperationException("Option repair identity is stale.");
        if(request.Contracts is null||request.Contracts.Length>2048||request.Contracts.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Invalid option repair contracts.",nameof(request));
        if(request.Contracts.Length==0)return new([],[]);
        // Binding reads are deliberately unavailable during retirement. They
        // cannot be a precondition for repairing that same unhealthy dataset.
        // Native recovery fences the requested owner/generation; after recovery
        // report only contracts proven present in the replacement source.
        var result=await RecoverMarketDataAsync(new(request.OperationId,options.Role,options.Dataset,options.WorkerInstanceId,options.GenerationId,request.ValueDate,"Requested native option repair"),token).ConfigureAwait(false);
        if(!result.Recovered)return new([], [result.Error??"Native option repair failed."]);
        var contracts=request.Contracts.Distinct(StringComparer.Ordinal).ToArray();
        var repaired=new List<string>();var errors=new List<string>();
        foreach(var contract in contracts) {
            var binding=backend.Runtime.ReadDatasetOwnerBinding(contract,true);
            if(binding is {Active:true,Healthy:true}&&binding.Generation==result.GenerationId)repaired.Add(contract);
            else errors.Add("Option observation is unavailable in the recovered native dataset: "+contract);
        }
        return new(repaired.ToArray(),errors.ToArray());
    }
    /// <summary>Accepts an opt-in Development fault through the same native
    /// recovery command authority. This observer cannot extend dataset recovery.</summary>
    public Task<bool> InjectDevelopmentOptionWorkerFaultAsync(MarketDataDevelopmentFaultRequest request,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(!services.GetRequiredService<IHostEnvironment>().IsDevelopment()||!services.GetRequiredService<IConfiguration>().GetValue<bool>("MarketDataRecovery:DevelopmentVerification:Enabled"))
            throw new InvalidOperationException("Live fault injection requires explicitly enabled Development verification.");
        var status=backend.Runtime.ReadDatasetOwnerBackendStatus(Ready);
        var current=status.Datasets.Single(dataset=>dataset.Role=="Options"&&dataset.Dataset=="GLBX.MDP3");
        if(request.OperationId==Guid.Empty||!current.Running||request.WorkerInstanceId!=current.WorkerInstanceId||request.GenerationId!=current.GenerationId||request.ValueDate!=current.ValueDate)
            throw new InvalidOperationException("Development fault owner is stale.");
        _=ObserveDevelopmentAsync(new(request.OperationId,current.Role,current.Dataset,current.WorkerInstanceId,current.GenerationId,request.ValueDate,"Injected native Development fault"));
        return Task.FromResult(true);
    }
    async Task ObserveDevelopmentAsync(MarketDataRecoveryRequest request)
    {
        try { await backend.Runtime.RecoverDatasetOwnerAsync(request).ConfigureAwait(false); }
        catch(Exception error){services.GetRequiredService<ILogger<NativeOwnerRuntimeOperations>>().LogError(error,"Native Development recovery observer failed; OperationId={Operation}",request.OperationId);}
    }

}
