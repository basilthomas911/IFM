using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Maps a committed parent snapshot to the exact worker ownership control contract.</summary>
public static class CommittedOptionChainOwnership
{
    public static WorkerOptionChainRelease Create(DurableSubscriptionSnapshot snapshot, WorkerOptionChainRequest chain)
    {
        var contracts = chain.Options.ToDictionary(x => x.Pricing.Contract.ContractId, x => x.Pricing.Contract, StringComparer.Ordinal);
        var leases = snapshot.Authorities.SelectMany(x => x.Leases)
            .Where(x => x.Ticker.AssetKind == SubscriptionAssetKind.FuturesOption && contracts.ContainsKey(x.Ticker.ContractId)
                && (x.Ticker.PricingPlanId is null || x.Ticker.PricingPlanId == chain.ScopeId)).ToArray();
        if (leases.Any(x => x.Ticker.Dataset != snapshot.Dataset || x.Ticker.UnderlyingContractId != contracts[x.Ticker.ContractId].UnderlyingContractId))
            throw new InvalidDataException("Committed ownership differs from the qualified physical contract.");
        var owners = leases.OrderBy(x => x.LeaseId).Select(x => new WorkerOptionChainOwner(x.LeaseId, [x.Ticker.ContractId])).ToImmutableArray();
        return new(chain.ScopeId, Guid.Empty, chain.GenerationId,
            new(snapshot.Revision, snapshot.Scope, owners, WorkerOptionChainRuntime.PhysicalDigest(chain.Options)));
    }
}
