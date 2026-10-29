// Application lease policy copied for native cancellation/clock ownership. Baseline unchanged.
﻿using TomasAI.IFM.Shared.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Retains requested option ownership independently of replaceable worker leases.</summary>
public sealed class NativeRecoveringOptionLeaseRegistry(
    IRecoverableOptionDiscovery discovery, DatasetWorkerAdmissionRegistry admissions,
    IndividualOptionRiskReader? riskReader = null, ILogger? logger = null, TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth? health = null, TimeProvider? timeProvider=null) : IAsyncDisposable
{
    readonly TimeProvider clock=timeProvider??TimeProvider.System;
    sealed class Registration(CompositionDiscoveryRequest request)
    {
        public CompositionDiscoveryRequest Request { get; set; } = request;
        public WorkerOptionChainRequest? Lease { get; set; }
        public string? LastFailure { get; set; }
    }

    readonly Dictionary<(TickerStreamOwner Owner, string Contract), Registration> registrations = [];
    readonly SemaphoreSlim serial = new(1, 1);
    readonly SemaphoreSlim wake = new(0, 1);
    readonly CancellationTokenSource stopping = new();
    readonly object lifecycle = new();
    Task? loop;
    bool disposed;

    /// <summary>Starts admission-driven recovery with periodic retries for unavailable option scopes.</summary>
    public void Start()
    {
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (loop is not null) return;
            admissions.Changed += Signal;
            loop = Task.Run(RunAsync);
        }
    }

    void Signal()
    {
        lock (lifecycle)
            if (!disposed && wake.CurrentCount == 0) wake.Release();
    }

    /// <summary>Retains an owner's reviewed request and attempts current-generation acquisition.</summary>
    public async Task AcquireAsync(TickerStreamOwner owner, string contractId, CompositionDiscoveryRequest request, CancellationToken cancellationToken=default)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);
        await serial.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var key = (owner, contractId);
            if (!registrations.TryGetValue(key, out var registration))
            {
                if (registrations.Count >= 4096) throw new InvalidOperationException("OptionMonitoring.CAPACITY.EXCEEDED");
                registrations.Add(key, registration = new(request));
            }
            try { await RestoreAsync(registration, linked.Token).ConfigureAwait(false); }
            catch (Exception error) { Report(registration, error, linked.Token); throw; }
        }
        finally { serial.Release(); }
    }

    /// <summary>Restores each pending owner independently; one failure cannot prevent other legs recovering.</summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var registration in registrations.Values)
            {
                try
                {
                    await RestoreAsync(registration, cancellationToken).ConfigureAwait(false);
                    registration.LastFailure = null;
                    if (registration.Lease is { } active) health?.Succeeded(active.ValueDate, active.GenerationId, "Lease/" + active.LeaseId);
                }
                catch (Exception error) when (!cancellationToken.IsCancellationRequested)
                {
                    Report(registration, error, cancellationToken);
                    if (registration.LastFailure != error.Message)
                        logger?.LogWarning(error, "Option lease recovery failed; Method={Method}; LeaseId={LeaseId}; ValueDate={ValueDate}",
                            nameof(ReconcileAsync), registration.Request.LeaseId, registration.Request.ValueDate);
                    registration.LastFailure = error.Message;
                }
            }
        }
        finally { serial.Release(); }
    }

    async Task RestoreAsync(Registration registration, CancellationToken token)
    {
        var request = registration.Request;
        var admitted = admissions.TryGet("GLBX.MDP3", out var current) && current.ValueDate == request.ValueDate;
        // Temporary fencing must not delete the renewal registration that Host is about to adopt.
        if (!admitted) throw new TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingException("OptionMonitoring.WORKER.NOT_ADMITTED");
        if (registration.Lease is { } old)
        {
            if (admitted && old.GenerationId == current.GenerationId && discovery.IsLeaseCurrent(old.LeaseId)) return;
            Forget(old);
            registration.Lease = null;
            if (old.GenerationId == current.GenerationId)
                registration.Request = request = request with { LeaseId = Guid.NewGuid() };
        }

        using var operation = new OptionFeedTrace("option.lease.restore", logger, $"{request.LeaseId}|{current.GenerationId}|{request.ValueDate:yyyy-MM-dd}");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        // Stable request identity makes an uncertain acknowledgement retry idempotent in this generation.
        var acquired = await discovery.AcquireAsync(request with
        {
            GenerationId = current.GenerationId, DeadlineUtc = clock.GetUtcNow().AddSeconds(60)
        }, deadline.Token, renewAutomatically: true).WaitAsync(deadline.Token).ConfigureAwait(false);
        if (acquired.Lease is not { } lease || acquired.Failure is not null)
        {
            if (acquired.Failure?.Code == "LeaseEnded") registration.Request = request with { LeaseId = Guid.NewGuid() };
            throw new TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingException(acquired.Failure?.Code ?? "OptionMonitoring.ACQUIRE.FAILED", acquired.Failure?.Detail);
        }
        if (!admissions.TryGet("GLBX.MDP3", out var after) || after != current)
        {
            Forget(lease);
            throw new TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingException("OptionMonitoring.SESSION.CHANGED");
        }
        registration.Lease = lease;
        riskReader?.Register(lease);
        operation.Complete();
    }

    /// <summary>Adopts Host-restored leases before new-generation admission, preserving desired monitoring ownership.</summary>
    public async Task AdoptRestoredLeasesAsync(IReadOnlyList<WorkerOptionChainRequest> restored, CancellationToken token,Action? activate=null)
    {
        await serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            foreach (var registration in registrations.Values)
            {
                var lease = restored.FirstOrDefault(lease => lease.LeaseId == registration.Request.LeaseId);
                if (lease is null) continue;
                registration.Lease = lease;
                riskReader?.Register(lease);
            }
            // Publish metadata only after every registration follows the new
            // generation, while reconcile/acquire/release remain excluded.
            activate?.Invoke();
        }
        finally { serial.Release(); }
    }

    void Report(Registration registration, Exception error, CancellationToken token)
    {
        admissions.TryGet("GLBX.MDP3", out var current);
        health?.Report(registration.Request.ValueDate, current.GenerationId != Guid.Empty ? current.GenerationId : registration.Lease?.GenerationId ?? Guid.Empty, "Lease/" + registration.Request.LeaseId, error, token);
    }

    void Forget(WorkerOptionChainRequest lease)
    {
        discovery.ForgetLease(lease.LeaseId);
        riskReader?.Remove(lease.LeaseId);
    }

    /// <summary>Removes desired ownership before release so recovery cannot resurrect a stopped feed.</summary>
    public async Task ReleaseAsync(TickerStreamOwner owner, string contractId,CancellationToken cancellationToken=default)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token);
        await serial.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (!registrations.Remove((owner, contractId), out var registration) || registration.Lease is not { } lease) return;
            try
            {
                if (admissions.TryGet("GLBX.MDP3", out var current) && current.GenerationId == lease.GenerationId)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(5));
                    var result = await discovery.ReleaseAsync(lease, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
                    if (result.Failure is not null) throw new InvalidOperationException($"OptionMonitoring.RELEASE.FAILED: {result.Failure.Code}");
                }
            }
            finally { Forget(lease); }
        }
        finally { serial.Release(); }
    }

    async Task RunAsync()
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                await wake.WaitAsync(TimeSpan.FromSeconds(2), stopping.Token).ConfigureAwait(false);
                await ReconcileAsync(stopping.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    /// <summary>Stops recovery and retires local lease references after the actor releases its streams.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (lifecycle)
        {
            if (disposed) return;
            disposed = true;
            admissions.Changed -= Signal;
        }
        await stopping.CancelAsync().ConfigureAwait(false);
        if (loop is not null) await loop.ConfigureAwait(false);
        await serial.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var registration in registrations.Values)
                if (registration.Lease is { } lease) Forget(lease);
            registrations.Clear();
        }
        finally { serial.Release(); }
        // Outstanding callers can still be completing cancellation; do not dispose their synchronization primitives.
    }
}
