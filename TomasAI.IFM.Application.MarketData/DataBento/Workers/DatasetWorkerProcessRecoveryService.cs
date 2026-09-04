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
    readonly DatabentoStage3Options options;
    readonly DatasetWorkerAdmissionRegistry admissions;
    readonly Func<DatabentoStage3Options, DatasetWorkerProcessSupervisor> supervisorFactory;
    readonly DatabentoTerminalFaultSignal? terminalFaultSignal;

    public DatasetWorkerProcessRecoveryService(
        DatabentoStage3Options options,
        DatasetWorkerAdmissionRegistry admissions,
        DatasetPublicationIngress? publicationIngress = null,
        DatabentoTerminalFaultSignal? terminalFaultSignal = null,
        Func<DatabentoStage3Options, DatasetWorkerProcessSupervisor>? supervisorFactory = null)
    {
        this.options = options.Validate();
        this.admissions = admissions ?? throw new ArgumentNullException(nameof(admissions));
        this.terminalFaultSignal = terminalFaultSignal;
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
            return entry.Supervisor.Current;
        });
        return await Task.WhenAll(queries).ConfigureAwait(false);
    }

    public async Task<DatasetWorkerProcessSnapshot> StartOwnedAsync(
        DatasetWorkerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Entry entry;
        lock (gate)
        {
            if (entries.ContainsKey(request.Dataset))
                throw new InvalidOperationException($"Dataset '{request.Dataset}' already has a supervised owner.");
            entry = new Entry(request, supervisorFactory(options));
            ObserveExit(entry.Supervisor);
            entries.Add(request.Dataset, entry);
        }

        try
        {
            var started = await entry.Supervisor.StartAsync(request, cancellationToken)
                .ConfigureAwait(false);
            admissions.Admit(ToAdmission(request));
            return started;
        }
        catch
        {
            lock (gate) entries.Remove(request.Dataset);
            await entry.Supervisor.DisposeAsync().ConfigureAwait(false);
            entry.Lifecycle.Dispose();
            throw;
        }
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
            if (entry.Request.ValueDate != request.ValueDate
                || entry.Request.GenerationId != request.ExpectedGenerationId)
                return Failed(request, "The replacement request does not match the admitted value date/generation.");

            // Close host admission before asking the old process to stop.  No output from the old
            // generation can mutate host state once replacement begins.
            admissions.Close(request.Dataset, request.ExpectedGenerationId);
            var stopped = await entry.Supervisor.StopAsync(cancellationToken).ConfigureAwait(false);
            if (stopped.Running)
                return Failed(request, "The previous worker process did not exit before replacement.");

            await entry.Supervisor.DisposeAsync().ConfigureAwait(false);
            var replacement = entry.Request with
            {
                WorkerInstanceId = Guid.NewGuid(),
                GenerationId = Guid.NewGuid()
            };
            var supervisor = supervisorFactory(options);
            ObserveExit(supervisor);
            try
            {
                await supervisor.StartAsync(replacement, cancellationToken).ConfigureAwait(false);
                var health = await supervisor.GetHealthAsync(cancellationToken).ConfigureAwait(false);
                if (!health.Healthy)
                    throw new InvalidOperationException($"Replacement did not qualify: {health.Detail}");
                entry.Request = replacement;
                entry.Supervisor = supervisor;
                admissions.Admit(ToAdmission(replacement));
                return new(request.Dataset, request.ExpectedGenerationId,
                    replacement.GenerationId, true,
                    stopped.ForcedTermination
                        ? "The unresponsive worker tree was forcibly terminated and replaced."
                        : "The worker exited and its replacement qualified.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                               || !cancellationToken.IsCancellationRequested)
            {
                await supervisor.DisposeAsync().ConfigureAwait(false);
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
            if (entry.Request.ValueDate != request.ValueDate
                || entry.Request.GenerationId != request.ExpectedGenerationId)
                return Failed(request, "The reset request does not match the admitted value date/generation.");
            admissions.Close(request.Dataset, request.ExpectedGenerationId);
            try
            {
                var reset = await entry.Supervisor.ResetAsync(cancellationToken).ConfigureAwait(false);
                var updated = entry.Request with { GenerationId = reset.GenerationId };
                entry.Request = updated;
                admissions.Admit(ToAdmission(updated));
                return new(request.Dataset, request.ExpectedGenerationId, reset.GenerationId,
                    reset.Healthy, reset.Detail);
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                               || !cancellationToken.IsCancellationRequested)
            {
                return Failed(request, $"Cooperative worker reset failed: {Bound(exception.Message)}");
            }
        }
        finally { entry.Lifecycle.Release(); }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        Entry[] snapshot;
        lock (gate) snapshot = entries.Values.ToArray();
        foreach (var entry in snapshot)
        {
            await entry.Lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                admissions.Close(entry.Request.Dataset, entry.Request.GenerationId);
                await entry.Supervisor.StopAsync(cancellationToken).ConfigureAwait(false);
                await entry.Supervisor.DisposeAsync().ConfigureAwait(false);
                lock (gate) entries.Remove(entry.Request.Dataset);
            }
            finally
            {
                entry.Lifecycle.Release();
                entry.Lifecycle.Dispose();
            }
        }
    }

    static DatasetWorkerAdmission ToAdmission(DatasetWorkerStartRequest request) => new(
        request.Dataset, request.ValueDate, request.WorkerInstanceId,
        request.GenerationId, request.ManifestRevision);

    static DatabentoDatasetResetResult Failed(DatabentoDatasetResetRequest request, string detail) =>
        new(request.Dataset, request.ExpectedGenerationId, Guid.Empty, false, Bound(detail));

    static string Bound(string value) => value.Length <= 4096 ? value : value[..4096];

    void ObserveExit(DatasetWorkerProcessSupervisor supervisor) =>
        supervisor.Exited += snapshot => terminalFaultSignal?.Notify(
            $"Dataset worker exited: dataset={snapshot.Dataset}; pid={snapshot.ProcessId}; "
            + $"generation={snapshot.GenerationId:D}; exitCode={snapshot.ExitCode?.ToString() ?? "unknown"}.");

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
