using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public readonly record struct DatasetWorkerAdmission(
    string Dataset,
    DateOnly ValueDate,
    Guid WorkerInstanceId,
    Guid GenerationId,
    long ManifestRevision);

/// <summary>Atomic host-side gate preventing stale worker generations from mutating current state.</summary>
public sealed class DatasetWorkerAdmissionRegistry : IRealtimeSourceAdmission
{
    readonly object gate = new();
    readonly Dictionary<string, AdmissionState> admissions = new(StringComparer.Ordinal);
    readonly Dictionary<(string Dataset, Guid Generation), AdmissionState> draining = new();
    readonly Dictionary<string, ProbeState> probes = new(StringComparer.Ordinal);
    bool managedRealtimeSources;
    long rejected;

    public event Action? Changed;

    public long RejectedPublications => Interlocked.Read(ref rejected);

    public void Admit(DatasetWorkerAdmission admission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.Dataset);
        if (admission.ValueDate == default || admission.WorkerInstanceId == Guid.Empty
            || admission.GenerationId == Guid.Empty || admission.ManifestRevision < 1)
            throw new ArgumentException("Dataset worker admission identity is invalid.", nameof(admission));
        var changed = false;
        lock (gate)
        {
            if (probes.ContainsKey(admission.Dataset))
                throw new InvalidOperationException("Candidate probe must be drained before full admission.");
            if (admissions.TryGetValue(admission.Dataset, out var previous))
            {
                // Repeating admission must not reset its publication replay fence or disconnect
                // cancellation from entries already queued in the downstream publisher.
                if (previous.Identity == admission) return;
                if (previous.ActiveHandlers != 0)
                    throw new InvalidOperationException(
                        "A replacement worker generation cannot be admitted while old realtime actor handlers are running.");
                Retire(admission.Dataset, previous);
            }
            admissions[admission.Dataset] = new AdmissionState(admission);
            managedRealtimeSources = true;
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    public void Close(string dataset, Guid expectedGeneration, Action? onClosed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        var changed = false;
        lock (gate)
        {
            if (admissions.TryGetValue(dataset, out var current)
                && current.Identity.GenerationId == expectedGeneration)
            {
                admissions.Remove(dataset);
                Retire(dataset, current);
                onClosed?.Invoke();
                changed = true;
            }
        }
        if (changed) Changed?.Invoke();
    }

    public bool TryAccept(
        DatasetWorkerAdmission identity,
        long publicationSequence) => TryAccept(identity, publicationSequence, out _);

    /// <summary>
    /// Returns the admission-lifetime token with acceptance. Publishers must retain this token on
    /// queued work: closing the generation fences items that passed ingress before the close.
    /// </summary>
    public bool TryAccept(
        DatasetWorkerAdmission identity,
        long publicationSequence,
        out CancellationToken generationCancellation)
    {
        generationCancellation = default;
        if (publicationSequence < 1)
            return Reject();
        lock (gate)
        {
            if (!admissions.TryGetValue(identity.Dataset, out var current)
                || current.Identity != identity
                || publicationSequence <= current.LastSequence)
                return Reject();
            current.LastSequence = publicationSequence;
            generationCancellation = current.Stopping.Token;
            return true;
        }
    }

    public bool TryGet(string dataset, out DatasetWorkerAdmission admission)
    {
        lock (gate)
        {
            if (admissions.TryGetValue(dataset, out var current))
            {
                admission = current.Identity;
                return true;
            }
            admission = default;
            return false;
        }
    }

    /// <summary>Holds an accepted generation while its actor handler runs; a hard reset drains this lease.</summary>
    public bool TryEnter(ActorSubject sourceSubject, string? dataset, Guid generationId,
        out IDisposable? processingLease)
    {
        processingLease = null;
        if (!IsSupervisedRealtimeRoute(sourceSubject)) return true;
        lock (gate)
        {
            if (!managedRealtimeSources) return true;
            if (IsProbeRoute(sourceSubject) && dataset is not null
                && probes.TryGetValue(dataset, out var probe)
                && probe.Identity.GenerationId == generationId && !probe.Closing)
            {
                probe.ActiveHandlers++;
                processingLease = new ProbeProcessingLease(this, probe);
                return true;
            }
            if (string.IsNullOrEmpty(dataset) || generationId == Guid.Empty
                || !admissions.TryGetValue(dataset, out var current)
                || current.Identity.GenerationId != generationId)
                return Reject();
            current.ActiveHandlers++;
            processingLease = new ProcessingLease(this, dataset, current);
            return true;
        }
    }

    /// <summary>Waits for handlers admitted before a generation was closed, within the caller's deadline.</summary>
    public async Task WaitForProcessingDrainAsync(string dataset, Guid generationId,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero) throw new TimeoutException("No budget remains to drain old actor handlers.");
        Task? pending;
        lock (gate)
            pending = draining.TryGetValue((dataset, generationId), out var state)
                ? state.Drained.Task : null;
        if (pending is not null) await pending.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    static bool IsSupervisedRealtimeRoute(ActorSubject subject) =>
        subject.ActorType == ActorType.Realtime
        && (subject.Name == FuturesTickTradeDataChangedEvent.Actor
            && subject.Verb is FuturesTickTradeDataChangedEvent.Verb
                or FuturesTickQuoteDataChangedEvent.Verb
                or FuturesSessionStatisticsUpdatedRealtimeEvent.Verb
            || subject.Name == FuturesMarketPriceUpdatedRealtimeEvent.Actor
            && subject.Verb is FuturesMarketPriceUpdatedRealtimeEvent.Verb
                or FuturesTradeReplayBatchRealtimeEvent.Verb
            || subject.Name == FuturesVwapSourceCheckpoint.Actor
            && subject.Verb == FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static bool IsProbeRoute(ActorSubject subject) =>
        subject.ActorType == ActorType.Realtime
        && subject.Name == FuturesTickTradeDataChangedEvent.Actor
        && subject.Verb is FuturesTickTradeDataChangedEvent.Verb
            or FuturesTickQuoteDataChangedEvent.Verb;

    /// <summary>Allows only one exact candidate generation through the tick-storage probe route.</summary>
    public void BeginProbe(DatasetWorkerAdmission identity)
    {
        if (string.IsNullOrWhiteSpace(identity.Dataset) || identity.GenerationId == Guid.Empty
            || identity.ValueDate == default || identity.WorkerInstanceId == Guid.Empty
            || identity.ManifestRevision < 1)
            throw new ArgumentException("Candidate probe identity is invalid.", nameof(identity));
        lock (gate)
        {
            if (admissions.ContainsKey(identity.Dataset) || probes.ContainsKey(identity.Dataset)
                || probes.Count >= 8)
                throw new InvalidOperationException("A candidate probe cannot overlap an admission or another probe.");
            probes.Add(identity.Dataset, new ProbeState(identity));
            managedRealtimeSources = true;
        }
    }

    /// <summary>Closes the probe and waits for every accepted tick-storage handler to finish.</summary>
    public async Task EndProbeAsync(DatasetWorkerAdmission identity, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        Task drained;
        lock (gate)
        {
            if (!probes.TryGetValue(identity.Dataset, out var probe) || probe.Identity != identity)
                throw new InvalidOperationException("The candidate probe identity differs.");
            probe.Closing = true;
            if (probe.ActiveHandlers == 0) probe.Drained.TrySetResult();
            drained = probe.Drained.Task;
        }
        await drained.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        lock (gate)
            if (probes.TryGetValue(identity.Dataset, out var probe) && probe.Identity == identity
                && probe.ActiveHandlers == 0)
                probes.Remove(identity.Dataset);
    }

    /// <summary>Reports whether the exact candidate still has a scoped tick-storage probe.</summary>
    public bool HasProbe(DatasetWorkerAdmission identity)
    {
        lock (gate)
            return probes.TryGetValue(identity.Dataset, out var probe) && probe.Identity == identity;
    }

    void Retire(string dataset, AdmissionState state)
    {
        state.Closing = true;
        if (state.ActiveHandlers != 0)
            draining[(dataset, state.Identity.GenerationId)] = state;
        else
            state.Drained.TrySetResult();
        // CancelAsync marks the token canceled before returning but executes registrations away
        // from this admission lock. A downstream callback must not block generation fencing.
        _ = CompleteRetirementAsync(state.Stopping, state.Stopping.CancelAsync());
    }

    static async Task CompleteRetirementAsync(CancellationTokenSource stopping, Task callbacks)
    {
        try { await callbacks.ConfigureAwait(false); }
        catch (Exception) { /* External cancellation callbacks cannot prevent containment. */ }
        finally { stopping.Dispose(); }
    }

    sealed class AdmissionState(DatasetWorkerAdmission identity)
    {
        public DatasetWorkerAdmission Identity { get; } = identity;
        public CancellationTokenSource Stopping { get; } = new();
        public long LastSequence;
        public int ActiveHandlers;
        public bool Closing;
        public TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed class ProbeState(DatasetWorkerAdmission identity)
    {
        public DatasetWorkerAdmission Identity { get; } = identity;
        public int ActiveHandlers;
        public bool Closing;
        public TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed class ProbeProcessingLease(DatasetWorkerAdmissionRegistry owner, ProbeState state) : IDisposable
    {
        int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.gate)
            {
                state.ActiveHandlers--;
                if (state.Closing && state.ActiveHandlers == 0) state.Drained.TrySetResult();
            }
        }
    }

    sealed class ProcessingLease(DatasetWorkerAdmissionRegistry owner, string dataset,
        AdmissionState state) : IDisposable
    {
        int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.gate)
            {
                state.ActiveHandlers--;
                if (state.Closing && state.ActiveHandlers == 0)
                {
                    owner.draining.Remove((dataset, state.Identity.GenerationId));
                    state.Drained.TrySetResult();
                }
            }
        }
    }

    bool Reject()
    {
        Interlocked.Increment(ref rejected);
        return false;
    }
}
