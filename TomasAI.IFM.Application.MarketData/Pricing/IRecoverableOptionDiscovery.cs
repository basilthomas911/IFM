namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Acquires qualified option scopes and retires generation-bound lease references.</summary>
public interface IRecoverableOptionDiscovery
{
    /// <summary>Acquires an acknowledged scope with automatic pricing and lease maintenance.</summary>
    Task<CompositionDiscoveryResult> AcquireAsync(CompositionDiscoveryRequest request, CancellationToken cancellationToken,
        bool refreshAutomatically = true, bool renewAutomatically = false);
    /// <summary>Releases a lease from its current worker.</summary>
    Task<WorkerOptionChainResult> ReleaseAsync(WorkerOptionChainRequest lease, CancellationToken cancellationToken);
    /// <summary>Forgets a retired lease without provider I/O.</summary>
    void ForgetLease(Guid leaseId);
    /// <summary>Reports whether the lease remains registered and unexpired.</summary>
    bool IsLeaseCurrent(Guid leaseId);
}
