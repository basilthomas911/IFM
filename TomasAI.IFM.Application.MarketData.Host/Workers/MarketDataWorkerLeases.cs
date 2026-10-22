using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Host;

public sealed partial class MarketDataWorkerHost
{
    // Desired monitoring leases belong to Host, independently of a replaceable child generation.
    readonly Dictionary<(Guid Worker, Guid Lease), WorkerOptionChainRequest> optionLeases = [];
    sealed record BusinessScope(WorkerOptionChainRequest Chain, WorkerOptionChainOwnership Ownership);
    readonly Dictionary<(Guid Worker, string Scope), BusinessScope> businessScopes = [];

    public async Task<WorkerOptionChainResult> ApplyCommittedOwnershipAsync(string role, Guid workerId,
        WorkerOptionChainRequest chain, WorkerOptionChainRelease update, CancellationToken token)
    {
        var worker = RequireWorker(role, workerId);
        lock (gate) if (!CanUseGeneration(role, worker.Current.Dataset, workerId, chain.GenerationId)) return LeaseUnavailable();
        var result = await worker.ApplyOptionChainOwnershipAsync(update, token).ConfigureAwait(false);
        lock (gate)
        {
            if (!CanUseGeneration(role, worker.Current.Dataset, workerId, chain.GenerationId)) return LeaseUnavailable();
            if (result.Failure is null && update.Ownership is { } ownership)
            {
                if (ownership.Owners.IsEmpty) businessScopes.Remove((workerId, chain.ScopeId));
                else businessScopes[(workerId, chain.ScopeId)] = new(chain, ownership);
            }
        }
        return result;
    }

    public async Task<WorkerOptionChainResult> AcquireOptionLeaseAsync(string role, Guid workerId,
        WorkerOptionChainRequest request, CancellationToken token)
    {
        var worker = RequireWorker(role, workerId);
        lock (gate)
        {
            if (!CanUseGeneration(role, worker.Current.Dataset, workerId, request.GenerationId))
                return LeaseUnavailable();
            foreach (var expired in optionLeases.Where(pair => pair.Value.LeaseExpiresAtUtc <= DateTimeOffset.UtcNow)
                .Select(pair => pair.Key).ToArray()) optionLeases.Remove(expired);
            if (optionLeases.Count >= 4096 && !optionLeases.ContainsKey((workerId, request.LeaseId)))
                return new(false, new("ChainCapacity", "Host", "", "Host option lease capacity reached."));
        }
        BusinessScope? business;
        lock (gate) businessScopes.TryGetValue((workerId, request.ScopeId), out business);
        // A restored business owner can refresh pricing before a new temporary lease
        // is attached. Authority comes from committed ownership, not caller claims.
        if (business is not null && business.Ownership.Owners.Length > 0)
        {
            var refreshed = await worker.AcquireOptionChainAsync(request with
                { LeaseId = business.Ownership.Owners[0].LeaseId,
                    ExpectedContextDigest = PricingSemanticHash.Compute(new { request.MaturityDate, request.SeparateContractConnection,
                        Options = business.Chain.Options.OrderBy(option => option.Pricing.Contract.ContractId, StringComparer.Ordinal).ToImmutableArray() }) }, token).ConfigureAwait(false);
            if (!refreshed.Active) return refreshed;
            request = request with { ExpectedContextDigest = refreshed.ContextDigest };
        }
        var result = await worker.AcquireOptionChainAsync(request, token).ConfigureAwait(false);
        lock (gate)
        {
            if (!CanUseGeneration(role, worker.Current.Dataset, workerId, request.GenerationId)) return LeaseUnavailable();
            if (result.Active) optionLeases[(workerId, request.LeaseId)] = request;
            if (result.Active && businessScopes.TryGetValue((workerId, request.ScopeId), out var owned))
                businessScopes[(workerId, request.ScopeId)] = owned with { Chain = request };
        }
        return result;
    }

    public async Task<WorkerOptionChainResult> ReleaseOptionLeaseAsync(string role, Guid workerId,
        WorkerOptionChainRelease request, CancellationToken token)
    {
        var worker = RequireWorker(role, workerId);
        lock (gate)
        {
            if (!optionLeases.TryGetValue((workerId, request.LeaseId), out var desired)
                || desired.GenerationId != request.GenerationId) return LeaseUnavailable();
            // Removing desired ownership first prevents a concurrent reset from restoring a released lease.
            optionLeases.Remove((workerId, request.LeaseId));
            if (!CanUseGeneration(role, worker.Current.Dataset, workerId, request.GenerationId)) return LeaseUnavailable();
        }
        return await worker.ReleaseOptionChainAsync(request, token).ConfigureAwait(false);
    }

    bool CanUseGeneration(string role, string dataset, Guid workerId, Guid generation)
        => identities.TryGetValue(role + "|" + dataset, out var owned) && owned == workerId
            && roleStates.TryGetValue(role + "|" + dataset, out var state)
            && state.Lifecycle == "Healthy" && state.GenerationId == generation;

    static WorkerOptionChainResult LeaseUnavailable()
        => new(false, new("Recovering", "Generation", "", "Dataset admission is closed or generation was retired."));

    async Task RestoreOptionLeasesAsync(string role, string dataset,
        Databento.Workers.DatasetWorkerProcessSupervisor worker, CancellationToken token)
    {
        WorkerOptionChainRequest[] desired;
        BusinessScope[] committed;
        // Capture one authoritative desired subscription manifest, just as futures restores its manifest.
        lock (gate)
        {
            desired = optionLeases.Where(pair => pair.Key.Worker == worker.Current.WorkerInstanceId
                && pair.Value.LeaseExpiresAtUtc > DateTimeOffset.UtcNow).Select(pair => pair.Value).ToArray();
            committed = businessScopes.Where(pair => pair.Key.Worker == worker.Current.WorkerInstanceId).Select(pair => pair.Value).ToArray();
        }
        foreach (var old in desired)
        {
            lock (gate)
                if (!optionLeases.ContainsKey((worker.Current.WorkerInstanceId, old.LeaseId))
                    || old.LeaseExpiresAtUtc <= DateTimeOffset.UtcNow) continue;
            var next = old with { GenerationId = worker.Current.GenerationId, ExpectedContextDigest = null,
                Options = old.Options.Select(option => option with { Pricing = option.Pricing with
                    { GenerationId = worker.Current.GenerationId } }).ToImmutableArray() };
            WorkerOptionChainResult restored;
            do
            {
                restored = await worker.AcquireOptionChainAsync(next, token).ConfigureAwait(false);
                if (restored.Failure?.Code == "UnderlyingQuoteUnavailable") await Task.Delay(50, token).ConfigureAwait(false);
            } while (restored.Failure?.Code == "UnderlyingQuoteUnavailable" && next.LeaseExpiresAtUtc > DateTimeOffset.UtcNow);
            bool stillDesired;
            lock (gate)
            {
                stillDesired = optionLeases.ContainsKey((worker.Current.WorkerInstanceId, next.LeaseId))
                    && next.LeaseExpiresAtUtc > DateTimeOffset.UtcNow;
                if (!stillDesired) optionLeases.Remove((worker.Current.WorkerInstanceId, next.LeaseId));
            }
            if (!stillDesired)
            {
                if (restored.Active)
                    await worker.ReleaseOptionChainAsync(new(next.ScopeId, next.LeaseId, next.GenerationId), token).ConfigureAwait(false);
                continue;
            }
            if (!restored.Active) throw new InvalidOperationException("Option lease restoration failed: " + restored.Failure?.Code);
            bool retained;
            lock (gate)
            {
                // A release/stop wins over an in-flight restore.
                retained = optionLeases.ContainsKey((worker.Current.WorkerInstanceId, next.LeaseId));
                if (retained)
                    optionLeases[(worker.Current.WorkerInstanceId, next.LeaseId)] = next;
            }
            if (!retained)
                await worker.ReleaseOptionChainAsync(new(next.ScopeId, next.LeaseId, next.GenerationId), token).ConfigureAwait(false);
        }
        foreach (var business in committed)
        {
            var bootstrap = business.Chain with { GenerationId = worker.Current.GenerationId, LeaseId = Guid.NewGuid(),
                LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(120), ExpectedContextDigest = null,
                Options = business.Chain.Options.Select(option => option with { Pricing = option.Pricing with
                    { GenerationId = worker.Current.GenerationId } }).ToImmutableArray() };
            WorkerOptionChainResult acquired;
            do
            {
                acquired = await worker.AcquireOptionChainAsync(bootstrap, token).ConfigureAwait(false);
                if (acquired.Failure?.Code == "UnderlyingQuoteUnavailable") await Task.Delay(50, token).ConfigureAwait(false);
            } while (acquired.Failure?.Code == "UnderlyingQuoteUnavailable");
            if (!acquired.Active) throw new InvalidOperationException("Committed option scope restoration failed: " + acquired.Failure?.Code);
            var restored = await worker.ApplyOptionChainOwnershipAsync(new(bootstrap.ScopeId, Guid.Empty, bootstrap.GenerationId, business.Ownership), token).ConfigureAwait(false);
            if (restored.Failure is not null) throw new InvalidOperationException("Committed option ownership restoration failed: " + restored.Failure.Code);
            await worker.ReleaseOptionChainAsync(new(bootstrap.ScopeId, bootstrap.LeaseId, bootstrap.GenerationId), token).ConfigureAwait(false);
            lock (gate) businessScopes[(worker.Current.WorkerInstanceId, bootstrap.ScopeId)] = business with { Chain = bootstrap };
        }
        if (role == "Options" && backendState?.AdoptOptions is { } adopt)
        {
            WorkerOptionChainRequest[] restored;
            lock (gate) restored = optionLeases.Where(pair => pair.Key.Worker == worker.Current.WorkerInstanceId
                && pair.Value.GenerationId == worker.Current.GenerationId).Select(pair => pair.Value).ToArray();
            await adopt(restored, token).ConfigureAwait(false);
        }
    }
}
