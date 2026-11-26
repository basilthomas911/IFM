using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Atomic host-side gate preventing stale worker generations from mutating current state.</summary>
public sealed class DatasetWorkerAdmissionRegistry : IRealtimeSourceAdmission
{
    readonly System.Threading.Lock gate = new();
    readonly Dictionary<string, AdmissionState> admissions = new(StringComparer.Ordinal);
    readonly Dictionary<(string Dataset, Guid Generation), AdmissionState> draining = new();
    readonly Dictionary<string, ProbeState> probes = new(StringComparer.Ordinal);
    readonly Dictionary<(string Dataset,Guid Generation),RecoveryAuthority> recoveries=[];
    bool managedRealtimeSources;
    long rejected;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003", Justification = "Preserve the existing worker exit and status subscriber delegate contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046", Justification = "Preserve the existing worker exit and status subscriber delegate contract.")]
    public event Action? Changed;

    public long RejectedPublications => Interlocked.Read(ref rejected);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Name the public request owning the invalid nested property.")]
    public void Admit(DatasetWorkerAdmission admission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.Dataset, nameof(admission));
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
                || publicationSequence <= current.LastSequence || current.PendingSequence != 0)
                return Reject();
            current.LastSequence = publicationSequence;
            generationCancellation = current.Stopping.Token;
            return true;
        }
    }

    /// <summary>Reserves one exact source without advancing the confirmed publication watermark.
    /// A failed capture retains its identity and prevents a successor from overtaking it.</summary>
    public async ValueTask<PublicationAdmission?> BeginPublicationAsync(DatasetWorkerAdmission identity,long sequence,string contentHash,CancellationToken token=default)
    {
        AdmissionState? state;
        lock(gate)
        {
            if(sequence<1||contentHash.Length!=64||!admissions.TryGetValue(identity.Dataset,out state)||state.Identity!=identity)
            {Reject();return null;}
        }
        await state.PublicationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock(gate)
            {
                if(state.Closing||state.CaptureSealed||!admissions.TryGetValue(identity.Dataset,out var current)||!ReferenceEquals(current,state)||sequence<=state.LastSequence)
                {Reject();state.PublicationGate.Release();return null;}
                if(state.PendingSequence!=0&&(state.PendingSequence!=sequence||state.PendingHash!=contentHash))
                    throw new InvalidDataException("A prior immutable dataset publication still owns capture; retry it before its successor.");
                state.PendingSequence=sequence;state.PendingHash=contentHash;
                return new PublicationAdmission(this,state,sequence,contentHash);
            }
        }
        catch {state.PublicationGate.Release();throw;}
    }
    public sealed class PublicationAdmission:IDisposable
    {
        readonly DatasetWorkerAdmissionRegistry owner;readonly AdmissionState state;readonly long sequence;readonly string hash;int disposed;
        internal PublicationAdmission(DatasetWorkerAdmissionRegistry owner,AdmissionState state,long sequence,string hash)
        {this.owner=owner;this.state=state;this.sequence=sequence;this.hash=hash;}
        public CancellationToken GenerationCancellation=>state.Stopping.Token;
        /// <summary>Called only after confirmed durable ownership. Retirement cannot erase an already captured fact.</summary>
        public void ConfirmCapture()=>ConfirmCaptureThrough(sequence);
        public void ConfirmCaptureThrough(long lastSequence)
        {
            if(lastSequence<sequence)throw new InvalidDataException("Confirmed capture group cannot regress its source sequence.");
            lock(owner.gate)
            {
                ObjectDisposedException.ThrowIf(disposed!=0,this);
                if(state.PendingSequence!=sequence||state.PendingHash!=hash)throw new InvalidDataException("Dataset capture reservation changed.");
                state.LastSequence=lastSequence;state.PendingSequence=0;state.PendingHash=string.Empty;
            }
        }
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)state.PublicationGate.Release();}
    }

    /// <summary>Stops fresh source admission while original required handlers drain. This does not cancel their authority.</summary>
    public async ValueTask SealCaptureAsync(DatasetWorkerAdmission identity,CancellationToken token)
    {
        AdmissionState? state;lock(gate){if(!admissions.TryGetValue(identity.Dataset,out state)||state.Identity!=identity)return;}
        await state.PublicationGate.WaitAsync(token).ConfigureAwait(false);
        try{lock(gate)state.CaptureSealed=true;}finally{state.PublicationGate.Release();}
    }
    /// <summary>Allows only a generation backed by the durable hub's retained required-source inventory.
    /// This never installs live worker identity, admits new source capture or updates read views.</summary>
    public IDisposable AllowDurableRecovery(string dataset,Guid generation)
    {
        if(string.IsNullOrWhiteSpace(dataset)||generation==Guid.Empty)throw new ArgumentException("Invalid durable recovery generation.");
        lock(gate)
        {
            if(!recoveries.TryGetValue((dataset,generation),out var state)){state=new(dataset,generation);recoveries.Add((dataset,generation),state);}
            state.Owners++;state.Closing=false;managedRealtimeSources=true;
            return new RecoveryOwnership(this,state);
        }
    }

    public bool TryEnterDurableRecovery(string dataset,Guid generation,out IDisposable? processingLease)
    {
        processingLease=null;lock(gate)
        {
            if(!recoveries.TryGetValue((dataset,generation),out var state)||state.Closing)return false;
            state.ActiveHandlers++;processingLease=new RecoveryProcessing(this,state);return true;
        }
    }

    public long LastConfirmedSequence(DatasetWorkerAdmission identity)
    {lock(gate)return admissions.TryGetValue(identity.Dataset,out var state)&&state.Identity==identity?state.LastSequence:-1;}

    /// <summary>Proves a duplicate latest-value publication was already delivered in this exact admission.</summary>
    public bool HasConfirmedOptionObservation(DatasetWorkerAdmission identity, string contract, long sequence,
        DateTimeOffset observedAt,DateTimeOffset calculatedAt,bool pricingUpdate)
    {
        lock(gate)return admissions.TryGetValue(identity.Dataset,out var state)&&state.Identity==identity
            &&state.Options.TryGetValue((contract,pricingUpdate),out var old)
            &&(old.Sequence>=sequence||observedAt<old.ObservedAt||observedAt==old.ObservedAt&&calculatedAt<old.CalculatedAt);
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

    /// <summary>Latest option values are ordered per contract, so another active contract cannot fence catch-up.</summary>
    public bool TryAcceptOptionObservation(DatasetWorkerAdmission identity, string contract, long sequence,
        DateTimeOffset observedAt, DateTimeOffset calculatedAt, bool pricingUpdate, out CancellationToken cancellation)
    {
        cancellation = default;
        lock (gate)
        {
            if (sequence < 1 || string.IsNullOrWhiteSpace(contract) || !admissions.TryGetValue(identity.Dataset, out var current)
                || current.Identity != identity) return Reject();
            if (current.Options.TryGetValue((contract, pricingUpdate), out var old)
                && (sequence <= old.Sequence || observedAt < old.ObservedAt
                    || observedAt == old.ObservedAt && calculatedAt < old.CalculatedAt)) return Reject();
            if (!current.Options.ContainsKey((contract, pricingUpdate)) && current.Options.Count >= 4096) return Reject();
            cancellation = current.Stopping.Token;
            return true;
        }
    }
    public void RecordOptionObservation(DatasetWorkerAdmission identity, string contract, long sequence,
        DateTimeOffset observedAt, DateTimeOffset calculatedAt, bool pricingUpdate)
    {
        lock (gate)
            if (admissions.TryGetValue(identity.Dataset, out var current) && current.Identity == identity)
                current.Options[(contract, pricingUpdate)] = (sequence, observedAt, calculatedAt);
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
            if(dataset is not null&&recoveries.TryGetValue((dataset,generationId),out var recovery)&&!recovery.Closing)
            {recovery.ActiveHandlers++;processingLease=new RecoveryProcessing(this,recovery);return true;}
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
        && (string.Equals(subject.Name,FuturesTickTradeDataChangedEvent.Actor,StringComparison.Ordinal)
            && subject.Verb is FuturesTickTradeDataChangedEvent.Verb
                or FuturesTickQuoteDataChangedEvent.Verb
                or FuturesSessionStatisticsUpdatedRealtimeEvent.Verb
            || string.Equals(subject.Name,FuturesMarketPriceUpdatedRealtimeEvent.Actor,StringComparison.Ordinal)
            && subject.Verb is FuturesMarketPriceUpdatedRealtimeEvent.Verb
                or FuturesTradeReplayBatchRealtimeEvent.Verb
            || string.Equals(subject.Name,FuturesVwapSourceCheckpoint.Actor,StringComparison.Ordinal)
            && string.Equals(subject.Verb,FuturesMarketPriceUpdatedRealtimeEvent.Verb,StringComparison.Ordinal));

    static bool IsProbeRoute(ActorSubject subject) =>
        subject.ActorType == ActorType.Realtime
        && string.Equals(subject.Name,FuturesTickTradeDataChangedEvent.Actor,StringComparison.Ordinal)
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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The owned background worker records or returns its operation failure while keeping subsequent independent work available.")]
    static async Task CompleteRetirementAsync(CancellationTokenSource stopping, Task callbacks)
    {
        try { await callbacks.ConfigureAwait(false); }
        catch (Exception error) { System.Diagnostics.Trace.TraceError("External cancellation callback failed during containment: {0}", error); }
        finally { stopping.Dispose(); }
    }

    internal sealed class AdmissionState(DatasetWorkerAdmission identity)
    {
        public Dictionary<(string Contract, bool PricingUpdate), (long Sequence, DateTimeOffset ObservedAt, DateTimeOffset CalculatedAt)> Options { get; } = [];
        public DatasetWorkerAdmission Identity { get; } = identity;
        public CancellationTokenSource Stopping { get; } = new();
        public long LastSequence;
        public long PendingSequence;
        public string PendingHash=string.Empty;
        public SemaphoreSlim PublicationGate {get;}=new(1,1);
        public int ActiveHandlers;
        public bool Closing;
        public bool CaptureSealed;
        public TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed class RecoveryAuthority(string dataset,Guid generation)
    {public string Dataset{get;}=dataset;public Guid Generation{get;}=generation;public int Owners,ActiveHandlers;public bool Closing;}
    sealed class RecoveryOwnership(DatasetWorkerAdmissionRegistry owner,RecoveryAuthority state):IDisposable
    {
        int disposed;
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;lock(owner.gate){state.Owners--;if(state.Owners==0){state.Closing=true;if(state.ActiveHandlers==0)owner.recoveries.Remove((state.Dataset,state.Generation));}}}
    }
    sealed class RecoveryProcessing(DatasetWorkerAdmissionRegistry owner,RecoveryAuthority state):IDisposable
    {
        int disposed;
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;lock(owner.gate){state.ActiveHandlers--;if(state.Closing&&state.ActiveHandlers==0)owner.recoveries.Remove((state.Dataset,state.Generation));}}
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
