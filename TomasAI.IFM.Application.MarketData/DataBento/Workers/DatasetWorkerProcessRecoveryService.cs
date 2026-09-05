using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Owns every supervised child by exact process handle.  It never decides when recovery is due;
/// the serialized watchdog is the only caller allowed to request replacement.
/// </summary>
public sealed class DatasetWorkerProcessRecoveryService :
    IDatabentoDatasetProcessRecovery, IAsyncDisposable
{
    sealed class Entry(
        DatasetWorkerStartRequest request,
        DatasetWorkerProcessSupervisor supervisor)
    {
        internal DatasetWorkerStartRequest Request = request;
        internal DatasetWorkerProcessSupervisor Supervisor = supervisor;
        internal readonly SemaphoreSlim Lifecycle = new(1, 1);
    }

    readonly object gate = new();
    readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    readonly SemaphoreSlim shutdown = new(1, 1);
    bool stopping;
    readonly DatabentoStage3Options options;
    readonly DatasetWorkerAdmissionRegistry admissions;
    readonly Func<DatabentoStage3Options, DatasetWorkerProcessSupervisor> supervisorFactory;
    readonly DatabentoTerminalFaultSignal? terminalFaultSignal;
    readonly DatasetWorkerCurrentValues? currentValues;
    public DatasetDesiredSubscriptionRegistry DesiredSubscriptions { get; }

    public DatasetWorkerProcessRecoveryService(
        DatabentoStage3Options options,
        DatasetWorkerAdmissionRegistry admissions,
        DatasetPublicationIngress? publicationIngress = null,
        DatabentoTerminalFaultSignal? terminalFaultSignal = null,
        Func<DatabentoStage3Options, DatasetWorkerProcessSupervisor>? supervisorFactory = null,
        DatasetDesiredSubscriptionRegistry? desiredSubscriptions = null,
        DatasetWorkerCurrentValues? currentValues = null)
    {
        this.options = options.Validate();
        this.admissions = admissions ?? throw new ArgumentNullException(nameof(admissions));
        this.terminalFaultSignal = terminalFaultSignal;
        this.currentValues = currentValues;
        DesiredSubscriptions = desiredSubscriptions ?? new DatasetDesiredSubscriptionRegistry();
        Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? ingress =
            publicationIngress is null
                ? null
                : async (publication, cancellationToken) =>
                    _ = await publicationIngress.AcceptAsync(publication, cancellationToken)
                        .ConfigureAwait(false);
        this.supervisorFactory = supervisorFactory
            ?? (value => new DatasetWorkerProcessSupervisor(value, ingress));
    }

    public IReadOnlyList<DatasetWorkerProcessSnapshot> Current
    {
        get
        {
            lock (gate)
                return entries.Values.Select(entry => entry.Supervisor.Current).ToArray();
        }
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
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
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
                                               || !cancellationToken.IsCancellationRequested) { }
            var current = entry.Supervisor.Current;
            currentValues?.SetDatasetHealth(new(current.Dataset, entry.Request.ValueDate,
                current.WorkerInstanceId, current.GenerationId, current.ManifestRevision), current.Healthy);
            return current;
        });
        return await Task.WhenAll(queries).ConfigureAwait(false);
    }

    public async Task<DatasetWorkerProcessSnapshot> StartOwnedAsync(
        DatasetWorkerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var supplied = request.Manifest?.Validate()
            ?? throw new ArgumentException("A parent-owned subscription manifest is required.", nameof(request));
        if (supplied.Dataset != request.Dataset || supplied.ValueDate != request.ValueDate)
            throw new ArgumentException("The requested dataset/value date and manifest differ.", nameof(request));
        if (!DesiredSubscriptions.TryGet(request.Dataset, request.ValueDate, out var desired))
            desired = DesiredSubscriptions.Set(request.Dataset, request.ValueDate, supplied.GetRegistrations());
        request = WithManifest(request, desired) with
        { PrefixArguments = Array.AsReadOnly(request.PrefixArguments.ToArray()) };
        Entry entry;
        lock (gate)
        {
            if (stopping) throw new InvalidOperationException("Dataset shutdown is in progress.");
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

    public async Task<DatabentoDatasetResetResult> ReplaceProcessAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken)
    {
        Entry? entry;
        lock (gate) entries.TryGetValue(request.Dataset, out entry);
        if (entry is null)
            return Failed(request, "The dataset has no supervised worker owner.");

        await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
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
        Entry? entry;
        lock (gate) entries.TryGetValue(request.Dataset, out entry);
        if (entry is null)
            return Failed(request, "The dataset has no supervised worker owner.");
        await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
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
            if (desired.Fingerprint == entry.Request.Manifest?.Fingerprint
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
            if (entry.Request.Manifest?.Fingerprint != desired.Fingerprint)
            {
                var applied = await entry.Supervisor.ApplyManifestAsync(desired, deadline.Token).ConfigureAwait(false);
                entry.Request = WithManifest(entry.Request, desired) with { GenerationId = applied.GenerationId };
            }
            if (DesiredSubscriptions.TryWithCurrent(desired, () =>
            {
                lock (gate)
                {
                    if (!IsOwned(entry)) throw new InvalidOperationException("Dataset ownership ended before admission.");
                    var current = entry.Supervisor.Current;
                    if (!current.Running || !current.Healthy
                        || current.ManifestRevision != desired.Revision
                        || current.ManifestFingerprint != desired.Fingerprint)
                        throw new InvalidOperationException("Worker has not qualified the complete current manifest.");
                    var admission = ToAdmission(entry.Request);
                    currentValues?.ActivateDataset(admission, desired.GetRegistrations());
                    currentValues?.SetDatasetHealth(admission, true);
                    admissions.Admit(admission);
                }
            })) return;
        }
    }

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

    void ObserveExit(DatasetWorkerProcessSupervisor supervisor) =>
        supervisor.Exited += snapshot =>
        {
            lock (gate) CloseAdmission(snapshot.Dataset, snapshot.GenerationId);
            terminalFaultSignal?.Notify(
            $"Dataset worker exited: dataset={snapshot.Dataset}; pid={snapshot.ProcessId}; "
            + $"generation={snapshot.GenerationId:D}; exitCode={snapshot.ExitCode?.ToString() ?? "unknown"}.");
        };

    public async ValueTask DisposeAsync()
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
    }
}
