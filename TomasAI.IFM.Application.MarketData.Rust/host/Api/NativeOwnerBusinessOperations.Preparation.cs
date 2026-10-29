using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

public sealed partial class NativeOwnerBusinessOperations
{
    /// <summary>Freezes an already native-qualified resident snapshot using the
    /// existing business persistence/hash contract. Storage never owns a source,
    /// context preparation task, renewal loop or native cancellation authority.</summary>
    public Task<CompositionPreparationResult> PrepareCachedCompositionAsync(
        CompositionPreparationKey key,OptionChainSnapshotRequest request,CancellationToken token=default)
        =>new CompositionPreparationService(new PreparationSnapshots(backend.Runtime),
            Get<ICompositionPreparationStore>(),clock).PrepareCachedAsync(key,OptionChainCache,request,token);

    /// <summary>Freezes a new business snapshot from native-owned discovery and
    /// event-driven capture. The store handles immutable evidence only; no C#
    /// admission registry, quote polling, renewal loop or recovery timer exists.</summary>
    public async Task<CompositionPreparationResult> PrepareCompositionAsync(CompositionPreparationKey key,
        CompositionMarketDataPlan plan,string horizon,DateTimeOffset deadline,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(plan);CompositionPreparationService.ValidateKey(key);
        var store=Get<ICompositionPreparationStore>();
        var saved=await store.ReadAsync(key,token).ConfigureAwait(false);
        if(saved is not null){CompositionPreparationService.Validate(saved);return saved.Snapshot.ValidUntilUtc>clock.GetUtcNow()?new(saved,null):PreparationFailure("AcceptedSnapshotExpired");}
        if(plan.SchemaVersion!=1||plan.Dataset!="GLBX.MDP3"||plan.Root!="ES"||!plan.ScopeComplete
            ||horizon is not("Daily" or "Weekly" or "Monthly")||deadline<=clock.GetUtcNow()
            ||plan.Options.IsDefault||plan.Futures.IsDefault||plan.Options.Length>2048||plan.Futures.Length>16)
            return PreparationFailure("CompositionUniverseUnqualified");
        RequireDate(plan.ValueDate);
        var preparations=new CompositionPreparationService(new PreparationSnapshots(backend.Runtime),store,clock);
        var status=backend.Runtime.ReadDatasetOwnerBackendStatus(true);
        WorkerOptionChainRequest? lease=null;string scope;Guid generation;
        if(plan.IncludeOptions){
            if(plan.Calendar is null||plan.Publication is null||plan.Conversion is null||!plan.Futures.IsEmpty)return PreparationFailure("PricingContextUnavailable");
            var owner=NativePreparationIdentity.For(key);
            var result=await AcquireOptionScopeCoreAsync(new(owner,status.Datasets.Single(dataset=>dataset.Role=="Options"&&dataset.Dataset==plan.Dataset).GenerationId,
                plan.ValueDate,plan.MaturityDate,deadline,plan.Options,true,plan.Calendar,plan.Publication,plan.Conversion),token,new(key.WorkflowId.ToString("D"),key.InputRevision,key.InputSha256)).ConfigureAwait(false);
            if(result.Failure is not null)return new(null,result.Failure);
            if(result.CompleteEmpty){
                // No physical quote source exists for a qualified empty universe.
                // Native Host identity fences this empty business result; no
                // provider identity, quote or reference observation is invented.
                return await preparations.PrepareEmptyAsync(key,PricingSemanticHash.Compute(plan.Options),horizon,
                    status.HostInstanceId,deadline,token).ConfigureAwait(false);
            }
            lease=result.Lease;if(lease is null)return PreparationFailure("ChainUnavailable");
            scope=lease.ScopeId;generation=lease.GenerationId;
        }else{
            if(!plan.Options.IsEmpty||plan.Futures.IsEmpty||plan.Futures.Any(future=>future.Root!=plan.Root||future.Dataset!=plan.Dataset))return PreparationFailure("ContractMetadataUnavailable");
            var nativeRoutes=await Get<NativeCompositionPlanPersistence>().RoutesAsync(plan.Futures.Select(future=>future.ContractId),token).ConfigureAwait(false);
            var reconstruction=new CompositionRoutePlan(1,"",plan.Dataset,default,[],plan.Futures.OrderBy(future=>future.ContractId,StringComparer.Ordinal).ToImmutableArray(),nativeRoutes,null,null,null).Seal();
            reconstruction.Validate();await Get<ICompositionRoutePlanStore>().SaveAsync(reconstruction,token).ConfigureAwait(false);
            scope=reconstruction.PlanId;
            generation=backend.Runtime.ReadDatasetOwnerBackendStatus(true).Datasets.Single(dataset=>dataset.Role=="Futures"&&dataset.Dataset==plan.Dataset).GenerationId;
            if(generation==Guid.Empty)return PreparationFailure("Recovering");
        }
        var request=new CompositionSnapshotRequest(Guid.NewGuid(),scope,horizon,generation,default,deadline,plan.IncludeOptions,Futures:plan.Futures);
        // A store exception is an uncertain business commit, so preserve native
        // desired ownership for explicit retry/reconciliation. A delayed failure
        // observer must not withdraw another attempt of the same immutable key.
        // Rust retires provisional demand from committed authority facts.
        var prepared=await preparations.PrepareAsync(key,plan.Dataset,request,token,
            lease is null?null:new(lease.ScopeId,lease.LeaseId,lease.GenerationId)).ConfigureAwait(false);
        return prepared;
    }
    static CompositionPreparationResult PreparationFailure(string code)=>new(null,
        new(code,"CompositionPreparation","","Reviewed complete market evidence is unavailable."));

    /// <summary>Capture-only port for the immutable business-evidence writer.
    /// Resource commands deliberately stay on the native desired-owner API;
    /// this writer must never become another acquisition/renewal coordinator.</summary>
    sealed class PreparationSnapshots(NativeMarketDataRuntime runtime):ICompositionMarketDataApi
    {
        public Task<CompositionSnapshotResult> CaptureAsync(string dataset,CompositionSnapshotRequest request,CancellationToken token)
            =>runtime.CaptureDatasetOwnerCompositionAsync(dataset,request,token,awaitQuotes:true);
        public Task<WorkerOptionChainResult> AcquireAsync(string dataset,WorkerOptionChainRequest request,CancellationToken token)
            =>throw new NotSupportedException("Immutable preparation storage cannot acquire native resources.");
        public Task<WorkerOptionChainResult> ReleaseAsync(string dataset,WorkerOptionChainRelease request,CancellationToken token)
            =>throw new NotSupportedException("Immutable preparation storage cannot release native resources.");
    }
}
