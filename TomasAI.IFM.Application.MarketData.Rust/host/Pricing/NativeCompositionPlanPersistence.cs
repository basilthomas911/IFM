using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Persists immutable business reconstruction facts after native
/// qualification. It owns no lease registry, renewal task, context provider,
/// acquisition gate or source handle. A stalled store cannot pin native reset.</summary>
public sealed class NativeCompositionPlanPersistence(NativeDatasetBackend backend,
    IDbContextFactory databases,ICompositionRoutePlanStore store)
{
    /// <summary>Uses only native accepted membership and copied underlying
    /// metadata. The business hash is not a provider/physical resource identity.</summary>
    public async Task<CompositionDiscoveryResult> BindAsync(CompositionDiscoveryRequest request,
        WorkerOptionChainRequest receipt,CancellationToken token)
    {
        var accepted=receipt.Options.Select(option=>option.Pricing.Contract.ContractId).ToHashSet(StringComparer.Ordinal);
        var options=request.Definitions.Where(candidate=>accepted.Contains(candidate.ContractId)).OrderBy(candidate=>candidate.ContractId,StringComparer.Ordinal).ToImmutableArray();
        if(options.Length!=accepted.Count)throw new InvalidDataException("Native admitted membership differs from the copied reconstruction facts.");
        var routes=await RoutesAsync(receipt.Options.Select(option=>option.Pricing.Contract.UnderlyingContractId),token).ConfigureAwait(false);
        var plan=new CompositionRoutePlan(1,"","GLBX.MDP3",request.MaturityDate,options,[],routes,
            request.Calendar,request.Publication,request.Conversion){SeparateContractConnection=request.SeparateContractConnection,IndependentChainConnection=request.IndependentChainConnection}.Seal();
        plan.Validate();
        await store.SaveAsync(plan,token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        backend.Runtime.AliasDatasetOwnerScope(receipt.LeaseId,receipt.GenerationId,receipt.ScopeId,plan.PlanId);
        // Copy again after the business await. A retired source cannot return
        // its old contexts as admission evidence for a replacement generation.
        return backend.Runtime.ReadDatasetOwnerDiscovery(receipt.LeaseId);
    }
    /// <summary>Copies immutable routes independently of source admission. The
    /// database fallback is metadata only and cannot hold a provider resource.</summary>
    public async Task<ImmutableArray<DatasetSubscriptionContract>> RoutesAsync(IEnumerable<string> contracts,CancellationToken token)
    {
        var current=backend.Runtime.ReadDatasetOwnerContracts(MarketDataLane.FuturesGlbx).ToDictionary(contract=>contract.ContractId,StringComparer.Ordinal);
        var result=ImmutableArray.CreateBuilder<DatasetSubscriptionContract>();
        foreach(var id in contracts.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) {
            var model=current.GetValueOrDefault(id)??await databases.SecuritiesDb.GetFuturesContractAsync(id,token).ConfigureAwait(false)
                ??throw new CompositionMarketSourceException("UnderlyingRouteUnavailable");
            result.Add(new(){DomainContractId=model.ContractId,ProviderContractName=model.LocalSymbol,AssetTypeId=AssetTypeId.Futures,
                RootSymbol=model.Symbol,Dataset="GLBX.MDP3",OnTheRun=false,Rollover=false});
        }
        return result.ToImmutable();
    }
}
