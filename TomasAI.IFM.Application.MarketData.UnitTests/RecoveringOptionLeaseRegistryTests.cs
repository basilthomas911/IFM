using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class RecoveringOptionLeaseRegistryTests
{
    static readonly DateOnly Date = new(2026, 10, 20);
    static readonly TickerStreamOwner Owner = new("OptionActor", "trade1501", "leg");
    static CompositionDiscoveryRequest Request() => new(Guid.NewGuid(), Guid.Empty, Date, Date.AddMonths(1),
        DateTimeOffset.UtcNow.AddMinutes(1), [], true, null!, null!, null!, SeparateContractConnection: true);
    static Guid Admit(DatasetWorkerAdmissionRegistry admissions, DateOnly? date = null)
    {
        var generation = Guid.NewGuid();
        admissions.Admit(new("GLBX.MDP3", date ?? Date, Guid.NewGuid(), generation, 1));
        return generation;
    }

    [Fact]
    public async Task Replacement_reacquires_all_owners_without_releasing_retired_worker()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); var old = Admit(admissions); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        await registry.AcquireAsync(Owner, "put", Request()); await registry.AcquireAsync(Owner, "call", Request());
        var next = Admit(admissions); await registry.ReconcileAsync(default);
        Assert.Equal(4, discovery.Acquired.Count); Assert.All(discovery.Acquired.TakeLast(2), x => Assert.Equal(next, x.GenerationId));
        Assert.Equal(2, discovery.Forgotten.Count); Assert.Empty(discovery.Released);
        Assert.NotEqual(old, next);
    }

    [Fact]
    public async Task Failed_leg_remains_pending_and_does_not_block_other_legs()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); Admit(admissions); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        var put = Request(); var call = Request();
        await registry.AcquireAsync(Owner, "put", put); await registry.AcquireAsync(Owner, "call", call);
        Admit(admissions); discovery.Fail = put.LeaseId;
        await registry.ReconcileAsync(default);
        Assert.Equal(3, discovery.Acquired.Count);
        discovery.Fail = null; await registry.ReconcileAsync(default);
        Assert.Equal(4, discovery.Acquired.Count);
        Assert.Equal(put.LeaseId, discovery.Acquired[^1].LeaseId);
    }

    [Fact]
    public async Task Closed_admission_preserves_requests_and_stop_prevents_resurrection()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); var generation = Admit(admissions); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        await registry.AcquireAsync(Owner, "put", Request()); await registry.AcquireAsync(Owner, "call", Request());
        admissions.Close("GLBX.MDP3", generation); await registry.ReconcileAsync(default);
        await registry.ReleaseAsync(Owner, "put"); Admit(admissions); await registry.ReconcileAsync(default);
        Assert.Equal(3, discovery.Acquired.Count); Assert.Empty(discovery.Released);
    }

    [Fact]
    public async Task Expired_maintenance_lease_is_recreated_in_same_generation()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); var generation = Admit(admissions); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        await registry.AcquireAsync(Owner, "put", Request()); discovery.Current.Clear();
        await registry.ReconcileAsync(default);
        Assert.Equal(2, discovery.Acquired.Count); Assert.Equal(generation, discovery.Acquired[^1].GenerationId);
    }

    [Fact]
    public async Task Separate_owners_release_independently_and_other_value_dates_are_not_replayed()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); Admit(admissions); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        var second = new TickerStreamOwner("OptionActor", "trade1502", "leg");
        await registry.AcquireAsync(Owner, "put", Request()); await registry.AcquireAsync(second, "put", Request());
        await registry.ReleaseAsync(Owner, "put"); Assert.Single(discovery.Released);
        Admit(admissions, Date.AddDays(1)); await registry.ReconcileAsync(default); Assert.Equal(2, discovery.Acquired.Count);
        Admit(admissions); await registry.ReconcileAsync(default); Assert.Equal(3, discovery.Acquired.Count);
    }

    [Fact]
    public async Task Admission_change_during_acquisition_cannot_install_old_generation()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); Admit(admissions); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        discovery.OnAcquire = () => { discovery.OnAcquire = null; Admit(admissions); };
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.AcquireAsync(Owner, "put", Request()));
        Assert.Single(discovery.Forgotten); await registry.ReconcileAsync(default); Assert.Equal(2, discovery.Acquired.Count);
    }

    [Fact]
    public async Task Background_loop_reacts_to_admission_and_disposal_stops_recovery()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); Admit(admissions); var discovery = new Discovery();
        var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        await registry.AcquireAsync(Owner, "put", Request()); registry.Start(); Admit(admissions);
        await discovery.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await registry.DisposeAsync(); var count = discovery.Acquired.Count;
        Admit(admissions); Assert.Equal(count, discovery.Acquired.Count);
    }

    [Fact]
    public async Task Initial_unavailable_worker_keeps_pending_intent_until_admission()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.AcquireAsync(Owner, "put", Request()));
        Assert.Empty(discovery.Acquired); Admit(admissions); await registry.ReconcileAsync(default);
        Assert.Single(discovery.Acquired); await registry.ReconcileAsync(default); Assert.Single(discovery.Acquired);
    }

    [Fact]
    public async Task Stop_during_inflight_recovery_removes_owner_after_acknowledgement()
    {
        var admissions = new DatasetWorkerAdmissionRegistry(); Admit(admissions); var discovery = new Discovery();
        await using var registry = new RecoveringOptionLeaseRegistry(discovery, admissions);
        await registry.AcquireAsync(Owner, "put", Request()); Admit(admissions);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        discovery.BeforeAcquire = async () => { entered.TrySetResult(); await complete.Task; };
        var restoring = registry.ReconcileAsync(default); await entered.Task;
        var releasing = registry.ReleaseAsync(Owner, "put"); Assert.False(releasing.IsCompleted);
        complete.TrySetResult(); await restoring; await releasing;
        Admit(admissions); await registry.ReconcileAsync(default);
        Assert.Equal(2, discovery.Acquired.Count); Assert.Single(discovery.Released);
    }

    sealed class Discovery : IRecoverableOptionDiscovery
    {
        public readonly List<WorkerOptionChainRequest> Acquired = [];
        public readonly List<Guid> Forgotten = [];
        public readonly List<Guid> Released = [];
        public readonly HashSet<Guid> Current = [];
        public readonly TaskCompletionSource Recovered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Guid? Fail;
        public Action? OnAcquire;
        public Func<Task>? BeforeAcquire;
        public async Task<CompositionDiscoveryResult> AcquireAsync(CompositionDiscoveryRequest request, CancellationToken cancellationToken,
            bool refreshAutomatically = true, bool renewAutomatically = false)
        {
            if (BeforeAcquire is not null) await BeforeAcquire();
            Assert.True(renewAutomatically);
            if (Fail == request.LeaseId) throw new InvalidOperationException("Transient subscription failure");
            var lease = new WorkerOptionChainRequest(request.LeaseId.ToString(), request.LeaseId, request.GenerationId,
                request.ValueDate, request.MaturityDate, request.DeadlineUtc, []);
            Acquired.Add(lease); Current.Add(lease.LeaseId); OnAcquire?.Invoke();
            if (Acquired.Count > 1) Recovered.TrySetResult();
            return new CompositionDiscoveryResult(lease, [], false, null);
        }
        public Task<WorkerOptionChainResult> ReleaseAsync(WorkerOptionChainRequest lease, CancellationToken cancellationToken)
        { Released.Add(lease.LeaseId); return Task.FromResult(new WorkerOptionChainResult(true, null)); }
        public void ForgetLease(Guid leaseId) { Current.Remove(leaseId); Forgotten.Add(leaseId); }
        public bool IsLeaseCurrent(Guid leaseId) => Current.Contains(leaseId);
    }
}
