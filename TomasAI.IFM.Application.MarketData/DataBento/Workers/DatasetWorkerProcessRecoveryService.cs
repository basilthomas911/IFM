using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Owns every supervised child by exact process handle.  It never decides when recovery is due;
/// the serialized watchdog is the only caller allowed to request replacement.
/// </summary>
public sealed class DatasetWorkerProcessRecoveryService :
    IDatabentoDatasetProcessRecovery, IAsyncDisposable, Pricing.ICompositionMarketDataApi, Subscriptions.ICommittedOptionOwnership
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The containing recovery service owns entries and disposes each Lifecycle semaphore after disposing its supervisor.")]
    sealed class Entry(
        DatasetWorkerStartRequest request,
        IDatasetWorkerProcess supervisor)
    {
        internal DatasetWorkerStartRequest Request = request;
        internal IDatasetWorkerProcess Supervisor = supervisor;
        internal readonly SemaphoreSlim Lifecycle = new(1, 1);
    }

    readonly System.Threading.Lock gate = new();
    readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    readonly SemaphoreSlim shutdown = new(1, 1);
    Task? disposal;
    bool stopping;
    bool hardContainmentUnverified;
    readonly DatabentoStage3Options options;
    readonly DatasetWorkerAdmissionRegistry admissions;
    readonly Func<DatabentoStage3Options, IDatasetWorkerProcess> supervisorFactory;
    readonly DatabentoTerminalFaultSignal? terminalFaultSignal;
    readonly DatasetWorkerCurrentValues? currentValues;
    readonly IDurableSubscriptionIntentStore? durableIntent;
    public DatasetDesiredSubscriptionRegistry DesiredSubscriptions { get; }

    public DatasetWorkerProcessRecoveryService(
        DatabentoStage3Options options,
        DatasetWorkerAdmissionRegistry admissions,
        DatasetPublicationIngress? publicationIngress = null,
        DatabentoTerminalFaultSignal? terminalFaultSignal = null,
        Func<DatabentoStage3Options, IDatasetWorkerProcess>? supervisorFactory = null,
        DatasetDesiredSubscriptionRegistry? desiredSubscriptions = null,
        DatasetWorkerCurrentValues? currentValues = null,
        IDurableSubscriptionIntentStore? durableIntent = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Validate();
        this.admissions = admissions ?? throw new ArgumentNullException(nameof(admissions));
        this.terminalFaultSignal = terminalFaultSignal;
        this.currentValues = currentValues;
        this.durableIntent = durableIntent;
        DesiredSubscriptions = desiredSubscriptions ?? new DatasetDesiredSubscriptionRegistry();
        Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? ingress =
            publicationIngress is null
                ? null
                : async (publication, cancellationToken) =>
                {
                    var accepted = await publicationIngress.AcceptAsync(publication, cancellationToken).ConfigureAwait(false);
                    if (!accepted && publication.Kind == DatasetPublicationKind.OptionTradeEvidence)
                        throw new InvalidOperationException("Option trade evidence failed worker-generation admission.");
                };
        this.supervisorFactory = supervisorFactory
            ?? (value => new DatasetWorkerProcessSupervisor(value, ingress));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2365", Justification = "This published diagnostic property returns an isolated snapshot rather than mutable internal state.")]
    public IReadOnlyList<DatasetWorkerProcessSnapshot> Current
    {
        get
        {
            lock (gate)
                return entries.Values.Select(entry => entry.Supervisor.Current).ToArray();
        }
    }

    public Task<Pricing.WorkerOptionChainResult> AcquireAsync(string dataset, Pricing.WorkerOptionChainRequest request, CancellationToken cancellationToken)
        
    {
        ArgumentNullException.ThrowIfNull(request);
        return UseGenerationAsync(dataset, request.GenerationId, worker => worker.AcquireOptionChainAsync(request, cancellationToken), cancellationToken);
    }

    public Task<Pricing.WorkerOptionChainResult> ReleaseAsync(string dataset, Pricing.WorkerOptionChainRelease request, CancellationToken cancellationToken)
        
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Ownership is not null
            ? throw new UnauthorizedAccessException("Business ownership must be read from committed intent by the parent adapter.")
            : UseGenerationAsync(dataset, request.GenerationId, worker => worker.ReleaseOptionChainAsync(request, cancellationToken), cancellationToken);
    }

    /// <summary>Derives worker ownership from committed PostgreSQL rows. Callers supply no owner, purpose or lease claim.</summary>
    public async Task<Pricing.WorkerOptionChainResult> ApplyBusinessOwnershipAsync(string authorityScope, string dataset,
        Pricing.WorkerOptionChainRequest chain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chain);
        var store = durableIntent ?? throw new InvalidOperationException("Durable subscription persistence is unavailable.");
        var snapshot = await store.ReadAsync(authorityScope, dataset, cancellationToken).ConfigureAwait(false);
        var update = Pricing.CommittedOptionChainOwnership.Create(snapshot, chain);
        return await UseGenerationAsync(dataset, chain.GenerationId,
            worker => worker.ApplyOptionChainOwnershipAsync(update, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public Task<Pricing.CompositionSnapshotResult> CaptureAsync(string dataset, Pricing.CompositionSnapshotRequest request, CancellationToken cancellationToken)
        
    {
        ArgumentNullException.ThrowIfNull(request);
        return UseGenerationAsync(dataset, request.GenerationId, worker => worker.CaptureCompositionSnapshotAsync(request, cancellationToken), cancellationToken);
    }

    async Task<T> UseGenerationAsync<T>(string dataset, Guid generation, Func<IDatasetWorkerProcess, Task<T>> operation, CancellationToken cancellationToken)
    {
        Entry entry;
        lock (gate)
        {
            if (stopping || !entries.TryGetValue(dataset, out entry!)) throw new Pricing.CompositionMarketSourceException("Recovering");
        }
        await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsOwned(entry) || !admissions.TryGet(dataset, out var admitted) || admitted.GenerationId != generation
                || !entry.Supervisor.Current.Healthy) throw new Pricing.CompositionMarketSourceException("Recovering");
            var result = await operation(entry.Supervisor).ConfigureAwait(false);
            if (!admissions.TryGet(dataset, out var after) || after != admitted)
                throw new Pricing.CompositionMarketSourceException("Recovering");
            return result;
        }
        finally { entry.Lifecycle.Release(); }
    }

    public bool HasExited(string dataset, Guid expectedGeneration)
    {
        lock (gate)
            return entries.TryGetValue(dataset, out var entry)
                && entry.Supervisor.Current is { Running: false, ExitCode: not null } snapshot
                && snapshot.GenerationId == expectedGeneration;
    }

    public async Task<IReadOnlyList<DatasetWorkerProcessSnapshot>> GetHealthAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        Entry[] snapshot;
        lock (gate) snapshot = entries.Values.ToArray();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var queries = snapshot.Select(async entry =>
        {
            try
            {
                _ = await entry.Supervisor.GetHealthAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                               || !cancellationToken.IsCancellationRequested)
            { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
            var current = entry.Supervisor.Current;
            currentValues?.SetDatasetHealth(new(current.Dataset, entry.Request.ValueDate,
                current.WorkerInstanceId, current.GenerationId, current.ManifestRevision), current.Healthy);
            return current;
        });
        return await Task.WhenAll(queries).ConfigureAwait(false);
    }

    public Task<DatasetWorkerProcessSnapshot> StartOwnedAsync(
        DatasetWorkerStartRequest request, CancellationToken cancellationToken = default) =>
        StartOwnedCoreAsync(request, admit: true, cancellationToken);

    /// <summary>Starts an owned worker without admitting its data to downstream consumers.</summary>
    public Task<DatasetWorkerProcessSnapshot> StartCandidateAsync(
        DatasetWorkerStartRequest request, CancellationToken cancellationToken = default) =>
        StartOwnedCoreAsync(request, admit: false, cancellationToken);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    async Task<DatasetWorkerProcessSnapshot> StartOwnedCoreAsync(
        DatasetWorkerStartRequest request,
        bool admit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var supplied = request.Manifest?.Validate()
            ?? throw new ArgumentException("A parent-owned subscription manifest is required.", nameof(request));
        if (!string.Equals(supplied.Dataset,request.Dataset,StringComparison.Ordinal) || supplied.ValueDate != request.ValueDate)
            throw new ArgumentException("The requested dataset/value date and manifest differ.", nameof(request));
        if (!DesiredSubscriptions.TryGet(request.Dataset, request.ValueDate, out var desired))
        {
            if (!admit) throw new InvalidOperationException("Frozen recovery manifest is no longer current.");
            desired = DesiredSubscriptions.Set(request.Dataset, request.ValueDate, supplied.GetRegistrations());
        }
        if (!admit && !string.Equals(desired.Fingerprint,supplied.Fingerprint,StringComparison.Ordinal))
            throw new InvalidOperationException("Candidate cannot replace its frozen recovery manifest.");
        request = WithManifest(request, desired) with
        { PrefixArguments = Array.AsReadOnly(request.PrefixArguments.ToArray()) };
        Entry entry;
        lock (gate)
        {
            if (stopping || hardContainmentUnverified)
                throw new InvalidOperationException("Dataset containment has not been verified; worker startup is fenced.");
            if (entries.ContainsKey(request.Dataset))
                throw new InvalidOperationException($"Dataset '{request.Dataset}' already has a supervised owner.");
            entry = new Entry(request, supervisorFactory(options));
            ObserveExit(entry.Supervisor);
            entries.Add(request.Dataset, entry);
        }

        var acquired = false;
        try
        {
            await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            if (!IsOwned(entry)) throw new InvalidOperationException("Dataset ownership ended before startup.");
            var started = await entry.Supervisor.StartAsync(request, cancellationToken)
                .ConfigureAwait(false);
            entry.Request = request with { GenerationId = started.GenerationId };
            if (admit)
                await ConvergeAndAdmitAsync(entry, cancellationToken).ConfigureAwait(false);
            return entry.Supervisor.Current;
        }
        catch
        {
            // Startup cancellation may have happened while shutdown held this entry. Serialize
            // cleanup too, and retain ownership if process containment cannot verify exit.
            if (!acquired)
            {
                await entry.Lifecycle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                acquired = true;
            }
            if (IsOwned(entry))
            {
                CloseAdmission(entry.Request.Dataset, entry.Request.GenerationId);
                await entry.Supervisor.DisposeAsync().ConfigureAwait(false);
                RemoveIfOwned(entry);
            }
            throw;
        }
        finally { if (acquired) entry.Lifecycle.Release(); }
    }

    /// <summary>Admits only the exact frozen, locally qualified candidate generation.</summary>
    public async Task AdmitCandidateAsync(DatasetSubscriptionManifest frozen,
        Guid expectedGenerationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        Entry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(frozen.Dataset, out entry!))
                throw new InvalidOperationException("Candidate worker is not owned.");
            if (hardContainmentUnverified)
                throw new InvalidOperationException("Dataset containment has not been verified; candidate admission is fenced.");
        }
        await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsOwned(entry) || entry.Request.GenerationId != expectedGenerationId
                || entry.Request.ValueDate != frozen.ValueDate
                || !string.Equals(entry.Request.Manifest?.Fingerprint,frozen.Fingerprint,StringComparison.Ordinal))
                throw new InvalidOperationException("Candidate identity differs from the frozen manifest.");
            if (!DesiredSubscriptions.TryWithCurrent(frozen, () =>
            {
                lock (gate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (hardContainmentUnverified || !IsOwned(entry))
                        throw new InvalidOperationException("Dataset containment is unverified; candidate admission is fenced.");
                    var current = entry.Supervisor.Current;
                    if (!current.Running || !current.Healthy || current.GenerationId != expectedGenerationId
                        || current.ManifestRevision != frozen.Revision
                        || !string.Equals(current.ManifestFingerprint,frozen.Fingerprint,StringComparison.Ordinal))
                        throw new InvalidOperationException("Candidate has not qualified the frozen manifest.");
                    var admission = ToAdmission(entry.Request);
                    currentValues?.ActivateDataset(admission, frozen.GetRegistrations());
                    currentValues?.SetDatasetHealth(admission, true);
                    admissions.Admit(admission);
                }
            }))
                throw new InvalidOperationException("Frozen manifest is no longer current.");
        }
        finally { entry.Lifecycle.Release(); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public async Task<DatabentoDatasetResetResult> ReplaceProcessAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Entry? entry;
        lock (gate)
        {
            if (hardContainmentUnverified)
                return Failed(request, "Dataset containment has not been verified; worker replacement is fenced.");
            entries.TryGetValue(request.Dataset, out entry);
        }
        if (entry is null)
            return Failed(request, "The dataset has no supervised worker owner.");

        await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (gate)
                if (hardContainmentUnverified)
                    return Failed(request, "Dataset containment has not been verified; worker replacement is fenced.");
            if (!IsOwned(entry)) return Failed(request, "The dataset owner has stopped.");
            if (entry.Request.ValueDate != request.ValueDate
                || entry.Request.GenerationId != request.ExpectedGenerationId)
                return Failed(request, "The replacement request does not match the admitted value date/generation.");

            // Close host admission before asking the old process to stop.  No output from the old
            // generation can mutate host state once replacement begins.
            CloseAdmission(request.Dataset, request.ExpectedGenerationId);
            var stopped = await entry.Supervisor.StopAsync(cancellationToken).ConfigureAwait(false);
            if (stopped.Running)
                return Failed(request, "The previous worker process did not exit before replacement.");

            await entry.Supervisor.DisposeAsync().ConfigureAwait(false);
            var replacement = entry.Request with
            {
                WorkerInstanceId = Guid.NewGuid(),
                GenerationId = Guid.NewGuid()
            };
            replacement = WithManifest(replacement, GetDesired(entry));
            var supervisor = supervisorFactory(options);
            ObserveExit(supervisor);
            // Keep the failed/new process object owned and queryable even if startup fails.
            // A later watchdog replacement must not encounter the disposed previous supervisor.
            entry.Supervisor = supervisor;
            entry.Request = replacement;
            try
            {
                var started = await supervisor.StartAsync(replacement, cancellationToken).ConfigureAwait(false);
                entry.Request = replacement with { GenerationId = started.GenerationId };
                var health = await supervisor.GetHealthAsync(cancellationToken).ConfigureAwait(false);
                if (!health.Healthy)
                    throw new InvalidOperationException($"Replacement did not qualify: {health.Detail}");
                entry.Supervisor = supervisor;
                await ConvergeAndAdmitAsync(entry, cancellationToken).ConfigureAwait(false);
                return new(request.Dataset, request.ExpectedGenerationId,
                    entry.Request.GenerationId, true,
                    stopped.ForcedTermination
                        ? "The unresponsive worker tree was forcibly terminated and replaced."
                        : "The worker exited and its replacement qualified.");
            }
            catch (Exception exception)
            {
                await supervisor.StopAsync(CancellationToken.None).ConfigureAwait(false);
                if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
                return Failed(request, $"Replacement failed qualification: {Bound(exception.Message)}");
            }
        }
        finally { entry.Lifecycle.Release(); }
    }

    public async Task<DatabentoDatasetResetResult> ResetOwnedAsync(
        DatabentoDatasetResetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Entry? entry;
        lock (gate)
        {
            if (hardContainmentUnverified)
                return Failed(request, "Dataset containment has not been verified; worker reset is fenced.");
            entries.TryGetValue(request.Dataset, out entry);
        }
        if (entry is null)
            return Failed(request, "The dataset has no supervised worker owner.");
        await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (gate)
                if (hardContainmentUnverified)
                    return Failed(request, "Dataset containment has not been verified; worker reset is fenced.");
            if (!IsOwned(entry)) return Failed(request, "The dataset owner has stopped.");
            if (entry.Request.ValueDate != request.ValueDate
                || entry.Request.GenerationId != request.ExpectedGenerationId)
                return Failed(request, "The reset request does not match the admitted value date/generation.");
            CloseAdmission(request.Dataset, request.ExpectedGenerationId);
            try
            {
                var desired = GetDesired(entry);
                var reset = await entry.Supervisor.ResetAsync(desired, cancellationToken).ConfigureAwait(false);
                var updated = WithManifest(entry.Request, desired) with { GenerationId = reset.GenerationId };
                entry.Request = updated;
                if (!reset.Healthy) return Failed(request, reset.Detail);
                await ConvergeAndAdmitAsync(entry, cancellationToken).ConfigureAwait(false);
                return new(request.Dataset, request.ExpectedGenerationId, entry.Request.GenerationId,
                    true, reset.Detail);
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                               || !cancellationToken.IsCancellationRequested)
            {
                return Failed(request, $"Cooperative worker reset failed: {Bound(exception.Message)}");
            }
        }
        finally { entry.Lifecycle.Release(); }
    }

    /// <summary>Called by the serialized lifecycle owner after contract authority changes.</summary>
    public async Task<DatasetWorkerProcessSnapshot> ApplyDesiredManifestAsync(
        string dataset, CancellationToken cancellationToken = default)
    {
        Entry entry;
        lock (gate) entry = entries.GetValueOrDefault(dataset)
            ?? throw new InvalidOperationException("Dataset has no supervised owner.");
        await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsOwned(entry)) throw new InvalidOperationException("The dataset owner has stopped.");
            var desired = GetDesired(entry);
            if (string.Equals(desired.Fingerprint,entry.Request.Manifest?.Fingerprint,StringComparison.Ordinal)
                && admissions.TryGet(dataset, out var admitted) && admitted == ToAdmission(entry.Request))
                return entry.Supervisor.Current;
            CloseAdmission(dataset, entry.Request.GenerationId);
            await ConvergeAndAdmitAsync(entry, cancellationToken).ConfigureAwait(false);
            return entry.Supervisor.Current;
        }
        finally { entry.Lifecycle.Release(); }
    }

    DatasetSubscriptionManifest GetDesired(Entry entry) =>
        DesiredSubscriptions.TryGet(entry.Request.Dataset, entry.Request.ValueDate, out var manifest)
            ? manifest : throw new InvalidOperationException("The authoritative dataset manifest is unavailable.");

    static DatasetWorkerStartRequest WithManifest(DatasetWorkerStartRequest request,
        DatasetSubscriptionManifest manifest) => request with
        { Manifest = manifest, ManifestRevision = manifest.Revision };

    async Task ConvergeAndAdmitAsync(Entry entry, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.WorkerQualificationTimeout);
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var desired = GetDesired(entry);
            if (!string.Equals(entry.Request.Manifest?.Fingerprint,desired.Fingerprint,StringComparison.Ordinal))
            {
                var applied = await entry.Supervisor.ApplyManifestAsync(desired, deadline.Token).ConfigureAwait(false);
                entry.Request = WithManifest(entry.Request, desired) with { GenerationId = applied.GenerationId };
            }
            if (DesiredSubscriptions.TryWithCurrent(desired, () =>
            {
                lock (gate)
                {
                    if (hardContainmentUnverified)
                        throw new InvalidOperationException("Dataset containment is unverified; worker admission is fenced.");
                    if (!IsOwned(entry)) throw new InvalidOperationException("Dataset ownership ended before admission.");
                    var current = entry.Supervisor.Current;
                    if (!current.Running || !current.Healthy
                        || current.ManifestRevision != desired.Revision
                        || !string.Equals(current.ManifestFingerprint,desired.Fingerprint,StringComparison.Ordinal))
                        throw new InvalidOperationException("Worker has not qualified the complete current manifest.");
                    var admission = ToAdmission(entry.Request);
                    currentValues?.ActivateDataset(admission, desired.GetRegistrations());
                    currentValues?.SetDatasetHealth(admission, true);
                    admissions.Admit(admission);
                }
            })) return;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Complete owned-resource cleanup and containment across failures; retain the original or aggregate diagnostic cause.")]
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        await shutdown.WaitAsync(cancellationToken).ConfigureAwait(false);
        Entry[] snapshot;
        lock (gate)
        {
            stopping = true;
            snapshot = entries.Values.ToArray();
        }
        try
        {
            List<Exception>? failures = null;
            foreach (var entry in snapshot)
            {
                // Cancellation may reject a shutdown before ownership, but must not abandon later
                // datasets after the shutdown batch has begun.
                await entry.Lifecycle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    CloseAdmission(entry.Request.Dataset, entry.Request.GenerationId);
                    // Once shutdown owns this entry, finish its bounded containment even if the
                    // original caller cancels. Never leave an owned child behind a disposed lock.
                    await entry.Supervisor.StopAsync(CancellationToken.None).ConfigureAwait(false);
                    await entry.Supervisor.DisposeAsync().ConfigureAwait(false);
                    RemoveIfOwned(entry);
                }
                catch (Exception exception)
                {
                    // Preserve a failed owner for diagnosis/retry, but still contain other datasets.
                    (failures ??= []).Add(exception);
                }
                finally
                {
                    entry.Lifecycle.Release();
                }
            }
            currentValues?.Stop();
            if (failures is not null) throw new AggregateException("One or more dataset workers failed shutdown.", failures);
        }
        finally
        {
            lock (gate) stopping = false;
            shutdown.Release();
        }
    }

    /// <summary>
    /// Fences every current generation before bounded hard-recovery containment. A timeout is
    /// unsafe: the caller must not start a replacement while an owned worker may still exist.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Complete owned-resource cleanup and containment across failures; retain the original or aggregate diagnostic cause.")]
    public async Task<HardWorkerContainmentResult> ContainForHardRecoveryAsync(
        TimeSpan deadline, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, TimeSpan.Zero);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        DatasetWorkerProcessSnapshot[] before;
        lock (gate)
        {
            // A timed-out StopAllAsync keeps running in the background. Do not permit a
            // candidate to start merely because that task eventually releases `stopping`.
            hardContainmentUnverified = true;
            before = entries.Values.Select(entry => entry.Supervisor.Current).ToArray();
            foreach (var worker in before)
                CloseAdmission(worker.Dataset, worker.GenerationId);
        }
        try
        {
            var stopOperation = StopAllAsync(cancellationToken);
            try { await stopOperation.WaitAsync(deadline, cancellationToken).ConfigureAwait(false); }
            catch
            {
                _ = stopOperation.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                throw;
            }
            var remaining = deadline - elapsed.Elapsed;
            await Task.WhenAll(before.Select(worker => admissions.WaitForProcessingDrainAsync(
                worker.Dataset, worker.GenerationId, remaining, cancellationToken))).ConfigureAwait(false);
            var isolated = Current.Count == 0;
            if (isolated)
            {
                lock (gate) hardContainmentUnverified = false;
            }
            return new(isolated, before.Select(worker => worker.ProcessId).ToArray(),
                isolated ? null : new InvalidOperationException("Owned worker remains after containment."));
        }
        catch (Exception exception)
        {
            return new(false, before.Select(worker => worker.ProcessId).ToArray(), exception);
        }
    }

    bool IsOwned(Entry entry)
    {
        lock (gate) return entries.TryGetValue(entry.Request.Dataset, out var current)
            && ReferenceEquals(entry, current);
    }

    void RemoveIfOwned(Entry entry)
    {
        lock (gate)
            if (IsOwned(entry)) entries.Remove(entry.Request.Dataset);
    }

    static DatasetWorkerAdmission ToAdmission(DatasetWorkerStartRequest request) => new(
        request.Dataset, request.ValueDate, request.WorkerInstanceId,
        request.GenerationId, request.ManifestRevision);

    static DatabentoDatasetResetResult Failed(DatabentoDatasetResetRequest request, string detail) =>
        new(request.Dataset, request.ExpectedGenerationId, Guid.Empty, false, Bound(detail));

    static string Bound(string value) => value.Length <= 4096 ? value : value[..4096];

    void CloseAdmission(string dataset, Guid generation) =>
        admissions.Close(dataset, generation, () => currentValues?.ClearDataset(dataset));

    void ObserveExit(IDatasetWorkerProcess supervisor) =>
        supervisor.Exited += snapshot =>
        {
            lock (gate) CloseAdmission(snapshot.Dataset, snapshot.GenerationId);
            terminalFaultSignal?.Notify(
            $"Dataset worker exited: dataset={snapshot.Dataset}; pid={snapshot.ProcessId}; "
            + $"generation={snapshot.GenerationId:D}; exitCode={snapshot.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}.");
        };

    /// <summary>Shares one cleanup operation across all DI aliases and concurrent disposal callers.</summary>
    public ValueTask DisposeAsync()
    {
        lock (gate)
            return new ValueTask(disposal ??= DisposeCoreAsync());
    }

    async Task DisposeCoreAsync()
    {
        await StopAllAsync().ConfigureAwait(false);
        Entry[] snapshot;
        lock (gate)
        {
            snapshot = entries.Values.ToArray();
            entries.Clear();
        }
        foreach (var entry in snapshot)
        {
            await entry.Supervisor.DisposeAsync().ConfigureAwait(false);
            entry.Lifecycle.Dispose();
        }
        shutdown.Dispose();
    }
}
