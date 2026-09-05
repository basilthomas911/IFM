using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatabentoSupervisedWorkerOptions
{
    public required string DotNetHostPath { get; init; }
    public required string WorkerAssemblyPath { get; init; }
    public FeedDeploymentProfile DeploymentProfile { get; init; } = FeedDeploymentProfile.Development;
    public FeedDataSourceMode DataSource { get; init; } = FeedDataSourceMode.Synthetic;
    public SyntheticFeedOptions Synthetic { get; init; } = new();
    public TimeSpan HostPublisherStopTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public DatabentoSupervisedWorkerOptions Validate()
    {
        if (HostPublisherStopTimeout <= TimeSpan.Zero || HostPublisherStopTimeout > TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("The host publisher stop timeout must be positive and no greater than one minute.");
        if (!Path.IsPathFullyQualified(DotNetHostPath) || !File.Exists(DotNetHostPath))
            throw new InvalidOperationException("The Stage 3 dotnet host path must be an existing absolute file.");
        if (!Path.IsPathFullyQualified(WorkerAssemblyPath) || !File.Exists(WorkerAssemblyPath))
            throw new InvalidOperationException("The Stage 3 worker assembly path must be an existing absolute file.");
        return this;
    }
}

/// <summary>
/// Stage 3 lifecycle adapter for the synthetic qualification profile.  It starts exactly one
/// worker process per configured dataset and exposes process health through the existing watchdog
/// contract.  Live-provider enablement is intentionally rejected until its complete manifest and
/// query-mirror qualification gate is accepted.
/// </summary>
public sealed class SupervisedDatabentoLifecycleRuntime(
    IDatabentoContractAuthority contractAuthority,
    IDatabentoContractRegistrationRegistry registrations,
    DatasetWorkerProcessRecoveryService workers,
    DatabentoSupervisedWorkerOptions workerOptions,
    TimeProvider timeProvider,
    ITickAggregationEventPublisher publisher) : IDatabentoLifecycleRuntime
{
    DateOnly? activeValueDate;
    bool ownsPublisher;
    bool publisherStopFailed;
    public DateOnly? ActiveValueDate => activeValueDate;

    public async Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken)
    {
        _ = await contractAuthority.ReconcileAsync(valueDate,
            nameof(SupervisedDatabentoLifecycleRuntime), cancellationToken).ConfigureAwait(false);
        var manifests = RefreshDesired(valueDate);
        if (activeValueDate == valueDate)
            foreach (var manifest in manifests)
                await workers.ApplyDesiredManifestAsync(manifest.Dataset, cancellationToken).ConfigureAwait(false);
    }

    IReadOnlyList<DatasetSubscriptionManifest> RefreshDesired(DateOnly valueDate) => registrations.Snapshot()
        .GroupBy(value => value.Dataset, StringComparer.Ordinal)
        .Select(dataset => workers.DesiredSubscriptions.Set(dataset.Key!, valueDate, dataset))
        .ToArray();

    public async Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken)
    {
        if (publisherStopFailed)
            throw new InvalidOperationException("The host publisher failed bounded shutdown; restart the host before starting another supervised session.");
        if (activeValueDate is not null || ownsPublisher)
            throw new InvalidOperationException("Supervised market data is already active.");
        var launch = workerOptions.Validate();
        var manifests = RefreshDesired(valueDate);
        if (manifests.Count == 0)
            throw new InvalidOperationException("No dataset contract manifest is available for Stage 3 startup.");
        try
        {
            // The API-host publisher is distinct from every child's pipe publisher. With no
            // in-process epoch, this lifecycle owns its initialization before any child admission.
            ownsPublisher = true;
            await publisher.StartAsync(cancellationToken).ConfigureAwait(false);
            foreach (var manifest in manifests)
            {
                await workers.StartOwnedAsync(new DatasetWorkerStartRequest
                {
                    ExecutablePath = launch.DotNetHostPath,
                    PrefixArguments = [launch.WorkerAssemblyPath,
                        "--deployment-profile", launch.DeploymentProfile.ToString(),
                        "--data-source", launch.DataSource.ToString(),
                        "--synthetic-record-count", launch.Synthetic.RecordCount.ToString(),
                        "--synthetic-records-per-second", launch.Synthetic.RecordsPerSecond.ToString(),
                        "--synthetic-start-sequence", launch.Synthetic.StartSequence.ToString()],
                    Dataset = manifest.Dataset,
                    ValueDate = valueDate,
                    WorkerInstanceId = Guid.NewGuid(),
                    GenerationId = Guid.NewGuid(),
                    ManifestRevision = manifest.Revision,
                    Manifest = manifest
                }, cancellationToken).ConfigureAwait(false);
            }
            activeValueDate = valueDate;
        }
        catch (Exception startupFailure)
        {
            try { await StopOwnedResourcesAsync().ConfigureAwait(false); }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("Supervised startup failed and cleanup did not complete cleanly.",
                    startupFailure, cleanupFailure);
            }
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Once shutdown starts, finish exact worker containment and bounded publisher cleanup.
        await StopOwnedResourcesAsync().ConfigureAwait(false);
    }

    async Task StopOwnedResourcesAsync()
    {
        List<Exception>? failures = null;
        try { await workers.StopAllAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        if (ownsPublisher)
        {
            try
            {
                // StopAll closes generation tokens before this drain, releasing normal pending
                // sends. A non-cooperative host transport must not make worker shutdown unbounded.
                using var deadline = new CancellationTokenSource(workerOptions.HostPublisherStopTimeout);
                var stopping = publisher.StopAsync(deadline.Token).AsTask();
                try { await stopping.WaitAsync(workerOptions.HostPublisherStopTimeout).ConfigureAwait(false); }
                catch
                {
                    _ = stopping.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    throw;
                }
                ownsPublisher = false;
            }
            catch (Exception exception)
            {
                publisherStopFailed = true;
                (failures ??= []).Add(exception);
            }
        }
        if (failures is not null)
            throw new AggregateException("Supervised worker/publisher cleanup did not complete cleanly.", failures);
        activeValueDate = null;
    }

    public async Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request, CancellationToken cancellationToken)
    {
        RefreshDesired(request.ValueDate);
        // Transport recovery remains under the serialized lifecycle owner. A recoverable outage
        // creates a fresh bounded publisher session; the failed session's backlog is never replayed.
        if (publisher is ITickAggregationPublisherDiagnostics diagnostics && diagnostics.GetSnapshot().CanRecover)
            await publisher.StartAsync(cancellationToken).ConfigureAwait(false);
        return await workers.ResetOwnedAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<DatabentoBulkWatchdogSnapshot> GetWatchdogSnapshotAsync(
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contractSnapshot = registrations.Snapshot();
        var snapshots = await workers.GetHealthAsync(timeout, cancellationToken).ConfigureAwait(false);
        var observedOnUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expectedDatasets = contractSnapshot.Select(value => value.Dataset).Distinct(StringComparer.Ordinal).ToArray();
        var complete = activeValueDate is not null && snapshots.Count != 0
            && expectedDatasets.Length == snapshots.Count
            && expectedDatasets.All(dataset => snapshots.Any(worker => worker.Dataset == dataset));
        var feeds = snapshots.Select(worker =>
        {
            var contracts = contractSnapshot.Where(value => string.Equals(
                value.Dataset, worker.Dataset, StringComparison.Ordinal)).ToArray();
            var diagnostics = worker.Diagnostics ?? DatasetWorkerDiagnostics.Unavailable(
                worker.Dataset, worker.GenerationId, "The worker supplied no native/managed diagnostics.", observedOnUtc);
            complete &= worker.ControlResponsive && diagnostics.Complete;
            return diagnostics.ToWatchdogStatus(contracts,
                worker.Running && worker.ControlResponsive && worker.DataPlaneHealthy);
        }).ToArray();
        return new DatabentoBulkWatchdogSnapshot
        {
            Complete = complete,
            NativeBackend = workerOptions.DataSource == FeedDataSourceMode.Synthetic
                ? "SupervisedSynthetic" : "SupervisedDatabentoLive",
            NativeAbiVersion = 3,
            NativeGeneration = feeds.FirstOrDefault()?.GenerationId ?? Guid.Empty,
            ObservedOnUtc = observedOnUtc,
            Feeds = feeds,
            FailureDetail = complete ? string.Empty : "One or more supervised dataset diagnostic observations are incomplete."
        };
    }
}
