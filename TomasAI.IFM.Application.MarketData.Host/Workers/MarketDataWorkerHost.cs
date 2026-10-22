using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Owns one exact child process per market-data role and dataset independently of the API process.</summary>
public sealed partial class MarketDataWorkerHost(IConfiguration configuration, ILogger<MarketDataWorkerHost> logger, MarketDataHostConnection connection,
    MarketDataHostState? backendState = null, TomasAI.IFM.Application.MarketData.Pricing.IOptionTradeEvidenceWriter? evidenceWriter = null) : IHostedService, IAsyncDisposable
{
    readonly object gate = new();
    readonly Dictionary<string, DatasetWorkerProcessSupervisor> workers = new(StringComparer.Ordinal);
    readonly Dictionary<string, WorkerPublicationRelay> publications = new(StringComparer.Ordinal);
    readonly HashSet<Guid> retired = [];
    readonly Dictionary<string, Guid> identities = new(StringComparer.Ordinal);
    readonly Dictionary<string, MarketDataWorkerStart> manifests = new(StringComparer.Ordinal);
    readonly HashSet<string> starting = new(StringComparer.Ordinal);
    volatile bool ready;
    bool stopping;
    /// <summary>Whether both named worker executables are staged and this coordinator accepts commands.</summary>
    public bool IsReady => ready;

    /// <summary>Returns role-specific process identities, without exposing launch secrets.</summary>
    public object Capture()
    {
        lock (gate) return new
        {
            Ready = ready,
            Workers = workers.Select(pair => new { Worker = pair.Key, Process = pair.Value.Current }).ToArray()
        };
    }

    /// <summary>Launches one fixed named apphost using a resolved manifest and fresh worker/generation identities.</summary>
    public async Task<DatasetWorkerProcessSnapshot> StartWorkerAsync(string role, MarketDataWorkerStart request, CancellationToken token)
    {
        var name = ValidateRole(role);
        ArgumentNullException.ThrowIfNull(request);
        var manifest = request.Manifest.Validate();
        var key = name + "|" + manifest.Dataset;
        if (request.WorkerInstanceId == Guid.Empty || request.GenerationId == Guid.Empty)
            throw new ArgumentException("Worker identity must not be empty.", nameof(request));
        var (relay, supervisor) = CreateWorker(name, request.WorkerInstanceId);
        lock (gate)
        {
            if (retired.Contains(request.WorkerInstanceId)) throw new InvalidOperationException("Worker lifetime was retired.");
            if (!ready || stopping) throw new InvalidOperationException("MarketData Host is not ready.");
            if (workers.ContainsKey(key) || !starting.Add(key))
                throw new InvalidOperationException($"The {name} worker must be stopped before a new generation starts.");
            workers.Add(key, supervisor);
            manifests[key] = request;
            identities.Add(key, request.WorkerInstanceId);
            publications.Add(key, relay);
        }
        try
        {
            var profile = configuration["MarketDataHost:DeploymentProfile"] ?? configuration["AppSettings:Databento:DeploymentProfile"] ?? "Development";
            var source = configuration["MarketDataHost:DataSource"] ?? configuration["AppSettings:Databento:DataSource"] ?? "DatabentoLive";
            if (!Enum.TryParse<FeedDeploymentProfile>(profile, out _) || !Enum.TryParse<FeedDataSourceMode>(source, out _))
                throw new InvalidOperationException("MarketData Host feed profile/source configuration is invalid.");
            var state = await supervisor.StartAsync(new()
            {
                ExecutablePath = WorkerPath(name, manifest.Dataset),
                PrefixArguments = ["--deployment-profile", profile, "--data-source", source, "--worker-role", name],
                Dataset = manifest.Dataset, ValueDate = manifest.ValueDate, Manifest = manifest,
                ManifestRevision = manifest.Revision, WorkerInstanceId = request.WorkerInstanceId, GenerationId = request.GenerationId
            }, token).ConfigureAwait(false);
            logger.LogInformation("MarketData child started; Role={Role}; Dataset={Dataset}; ProcessId={ProcessId}; Generation={Generation}",
                name, state.Dataset, state.ProcessId, state.GenerationId);
            await PublishStateAsync(name, state.Dataset, "Healthy", null, state, token).ConfigureAwait(false);
            return state;
        }
        catch
        {
            relay.Close();
            await supervisor.TerminateAsync().ConfigureAwait(false);
            lock (gate) { workers.Remove(key); publications.Remove(key); identities.Remove(key); }
            await supervisor.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally { lock (gate) starting.Remove(key); }
    }

    /// <summary>Confirms exit of only the named child, including when its control loop is unresponsive.</summary>
    public async Task StopWorkerAsync(string role, Guid? expectedWorker = null)
    {
        var name = ValidateRole(role);
        KeyValuePair<string, DatasetWorkerProcessSupervisor>[] owned;
        lock (gate)
        {
            if (expectedWorker is { } expected) retired.Add(expected);
            owned = workers.Where(pair => pair.Key.StartsWith(name + "|", StringComparison.Ordinal)
                && (expectedWorker is null || identities[pair.Key] == expectedWorker)).ToArray();
            foreach (var pair in owned) publications[pair.Key].Close();
            foreach (var pair in owned) backendState?.Fence(name, pair.Value.Current.Dataset, pair.Value.Current.GenerationId);
        }
        foreach (var pair in owned)
        {
            await pair.Value.TerminateAsync().ConfigureAwait(false);
            bool dispose;
            lock (gate)
            {
                foreach (var lease in optionLeases.Keys.Where(lease => lease.Worker == pair.Value.Current.WorkerInstanceId).ToArray())
                    optionLeases.Remove(lease);
                foreach (var scope in businessScopes.Keys.Where(scope => scope.Worker == pair.Value.Current.WorkerInstanceId).ToArray())
                    businessScopes.Remove(scope);
                dispose = !starting.Contains(pair.Key);
                if (dispose && workers.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value))
                { workers.Remove(pair.Key); publications.Remove(pair.Key); identities.Remove(pair.Key); }
            }
            if (dispose) await pair.Value.DisposeAsync().ConfigureAwait(false);
            await PublishStateAsync(name, pair.Value.Current.Dataset, "Stopped", null, pair.Value.Current,
                CancellationToken.None).ConfigureAwait(false);
            logger.LogWarning("MarketData child stopped; Worker={Worker}; OtherDatasetsUnaffected={OtherDatasetsUnaffected}", pair.Key, true);
        }
    }

    /// <summary>Resolves the exact role and process lifetime independently of other datasets.</summary>
    public DatasetWorkerProcessSupervisor RequireWorker(string role, Guid? expectedWorker = null)
    {
        var name = ValidateRole(role);
        lock (gate) return workers.Single(pair => pair.Key.StartsWith(name + "|", StringComparison.Ordinal)
            && (expectedWorker is null || identities[pair.Key] == expectedWorker)).Value;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ready = File.Exists(WorkerPath("Futures", "GLBX.MDP3")) && File.Exists(WorkerPath("Futures", "XCBF.PITCH"))
            && File.Exists(WorkerPath("Options", "GLBX.MDP3"));
        if (!ready) logger.LogWarning("MarketData workers are not staged; WorkerDirectory={WorkerDirectory}", WorkerDirectory());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (gate) { ready = false; stopping = true; }
        await Task.WhenAll(StopWorkerAsync("Futures"), StopWorkerAsync("Options")).ConfigureAwait(false);
    }

    (WorkerPublicationRelay Relay, DatasetWorkerProcessSupervisor Supervisor) CreateWorker(string role, Guid workerId)
    {
        var relay = new WorkerPublicationRelay(connection, role, workerId, evidenceWriter);
        var supervisor = new DatasetWorkerProcessSupervisor(new DatabentoStage3Options(), (frame, ct) =>
        {
            if (role == "Futures") backendState?.Accept(frame);
            return role == "Options" && frame.Kind is not (DatasetPublicationKind.OptionQuoteObservation or DatasetPublicationKind.OptionTradeEvidence)
                ? ValueTask.CompletedTask : relay.PublishAsync(frame, ct);
        });
        return (relay, supervisor);
    }

    // Preserve Host-owned desired leases and logical worker identity; replace the OS process and all pipes.
    async Task<DatasetWorkerProcessSupervisor> ReplaceRecoveryWorkerAsync(string role, string dataset,
        DatasetWorkerProcessSupervisor old, MarketDataWorkerStart desired, CancellationToken deadline)
    {
        var key = role + "|" + dataset;
        lock (gate)
        {
            if (stopping || !workers.TryGetValue(key, out var current) || !ReferenceEquals(current, old))
                throw new OperationCanceledException("Recovery worker was stopped or replaced.");
            publications[key].Close();
            backendState?.Fence(role, dataset, old.Current.GenerationId);
        }
        await old.TerminateAsync().WaitAsync(deadline).ConfigureAwait(false);
        await old.DisposeAsync().AsTask().WaitAsync(deadline).ConfigureAwait(false);
        var (relay, next) = CreateWorker(role, desired.WorkerInstanceId);
        lock (gate)
        {
            if (stopping || !workers.TryGetValue(key, out var current) || !ReferenceEquals(current, old))
                throw new OperationCanceledException("Recovery ownership changed during termination.");
            workers[key] = next;
            publications[key] = relay;
        }
        var profile = configuration["MarketDataHost:DeploymentProfile"] ?? configuration["AppSettings:Databento:DeploymentProfile"] ?? "Development";
        var source = configuration["MarketDataHost:DataSource"] ?? configuration["AppSettings:Databento:DataSource"] ?? "DatabentoLive";
        await next.RunWithRecoveryDeadlineAsync(deadline, budget => next.StartAsync(new()
        {
            ExecutablePath = WorkerPath(role, dataset),
            PrefixArguments = ["--deployment-profile", profile, "--data-source", source, "--worker-role", role],
            Dataset = dataset, ValueDate = desired.Manifest.ValueDate, Manifest = desired.Manifest,
            ManifestRevision = desired.Manifest.Revision, WorkerInstanceId = desired.WorkerInstanceId, GenerationId = Guid.NewGuid()
        }, budget)).ConfigureAwait(false);
        logger.LogInformation("Dataset process replaced; Role={Role}; Dataset={Dataset}; ProcessId={ProcessId}", role, dataset, next.Current.ProcessId);
        return next;
    }

    string WorkerDirectory() => Path.GetFullPath(configuration["MarketDataHost:WorkerDirectory"]
        ?? Path.Combine(AppContext.BaseDirectory, "Workers"));
    string WorkerPath(string role, string dataset)
    {
        if (dataset.Length > 64 || dataset.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_'))
            throw new ArgumentException("Dataset cannot form a worker executable name.", nameof(dataset));
        return Path.Combine(WorkerDirectory(), "TomasAI.IFM.Application.MarketData.Host.Worker." + role + "-" + dataset
            + (OperatingSystem.IsWindows() ? ".exe" : ""));
    }
    static string ValidateRole(string role) => role is "Futures" or "Options" ? role
        : throw new ArgumentException("Worker role must be Futures or Options.", nameof(role));
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None).ConfigureAwait(false);
}
