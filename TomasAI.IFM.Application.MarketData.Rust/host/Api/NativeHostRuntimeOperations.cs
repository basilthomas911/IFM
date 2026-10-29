using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Rust.Api;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Concrete process-free Host control and mirror operations. Only the
/// existing IMarketDataApi contract crosses the Host boundary.</summary>
public sealed class NativeHostRuntimeOperations(IServiceProvider services,NativeMarketDataEpochFactory epochs,
    NativeHostState state,NativeDatasetRecovery recovery,NativeSessionPublications publications,NativeHostApplicationServices application,
    IDatabentoContractRegistrationRegistry registrations,TimeProvider clock,IHostEnvironment environment,IConfiguration configuration)
{
    bool Ready=>services.GetRequiredService<NativeMarketDataNatsService>().IsReady;
    /// <summary>Publishes current application ownership identities before qualified
    /// discovery runs. This reads local state and performs no acquisition.</summary>
    public void ObserveIdentity()=>state.Capture(Ready);
    /// <summary>Observes backend status, saved intent and retained processing failures.</summary>
    public Task<MarketDataBackendStatus> GetBackendStatusAsync(CancellationToken token=default)
    {token.ThrowIfCancellationRequested();return Task.FromResult(state.Capture(Ready,state.ValueDate is{} date?application.EnabledTrades(date):[]));}
    /// <summary>Provides bounded latest catch-up data for the API's existing mirror.</summary>
    public async Task<MarketDataReadSnapshot> GetBackendReadSnapshotAsync(CancellationToken token=default)
    {
        var status=await GetBackendStatusAsync(token).ConfigureAwait(false);
        var manifests=status.ValueDate is not null?state.CapturePrimaryManifests():[];
        return new(status,manifests,application.PreparedContracts(),publications.Current?.CaptureCurrent()??[],
            services.GetRequiredService<TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache>().Export(),BootstrapPrices:services.GetRequiredService<NativeBootstrapPrices>().Capture(status.ValueDate),OptionObservations:publications.Current?.CaptureOptions()??[],NativeOptionWorkload:epochs.TryGetCurrent(out var epoch)?epoch!.CaptureOptionWorkload():null);
    }
    /// <summary>Recovers one logical dataset without a process reset.</summary>
    public Task<MarketDataRecoveryResult> RecoverMarketDataAsync(MarketDataRecoveryRequest request,CancellationToken token=default)=>recovery.RecoverAsync(request,token);
    /// <summary>Resets configured roles concurrently under one original deadline.</summary>
    public async Task ResetMarketDataAsync(DateOnly date,Guid operation,CancellationToken token=default)
    {
        var status=await GetBackendStatusAsync(token).ConfigureAwait(false);
        if(status.ValueDate!=date)throw new InvalidOperationException("Reset ValueDate differs from the API epoch.");
        var deadline=clock.GetUtcNow().AddMinutes(2);using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(TimeSpan.FromMinutes(2));
        var results=await Task.WhenAll(status.Datasets.Select(item=>recovery.RecoverAsync(new(operation,item.Role,item.Dataset,item.WorkerInstanceId,item.GenerationId,date,"Requested native reset",DeadlineUtc:deadline),bounded.Token))).ConfigureAwait(false);
        if(results.Any(x=>!x.Recovered))throw new InvalidOperationException(string.Join("; ",results.Where(x=>!x.Recovered).Select(x=>x.Error)));
    }
    /// <summary>Repairs only requested option scopes; futures owners are unchanged.</summary>
    public async Task<OptionObservationRepairResult> RepairOptionObservationsAsync(OptionObservationRepairRequest request,CancellationToken token=default)
    {
        var epoch=epochs.Current;
        if(request.ValueDate!=epoch.ValueDate||request.GenerationId!=epoch.OptionGeneration)throw new InvalidOperationException("Option repair identity is stale.");
        var remaining=request.DeadlineUtc-clock.GetUtcNow();if(remaining>TimeSpan.FromMinutes(2))remaining=TimeSpan.FromMinutes(2);
        if(remaining<=TimeSpan.Zero)return new([], ["Option repair deadline expired."]);
        try{return new(await epoch.ResetOptionContractsAsync(request.Contracts,remaining,token).ConfigureAwait(false),[]);}
        catch(Exception error)when(!token.IsCancellationRequested){state.ReportProcessingFailure(nameof(RepairOptionObservationsAsync),error);return new([], [error.Message]);}
    }
    /// <summary>Injects a classified processing failure into the real health and
    /// recovery path. There is no child process to hang or terminate in Rust.</summary>
    public Task<bool> InjectDevelopmentOptionWorkerFaultAsync(MarketDataDevelopmentFaultRequest request,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(!environment.IsDevelopment()||!configuration.GetValue<bool>("MarketDataRecovery:DevelopmentVerification:Enabled"))
            throw new InvalidOperationException("Live fault injection requires explicitly enabled Development verification.");
        var current=state.Capture(Ready).Datasets.Single(x=>x.Role=="Options"&&x.Dataset=="GLBX.MDP3");
        if(request.OperationId==Guid.Empty||!current.Running||request.WorkerInstanceId!=current.WorkerInstanceId||request.GenerationId!=current.GenerationId||request.ValueDate!=current.ValueDate)throw new InvalidOperationException("Development fault owner is stale.");
        state.ReportProcessingFailure("DevelopmentInjection",new InvalidOperationException("Injected native option processing failure: "+request.OperationId));return Task.FromResult(true);
    }
}
