using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatabentoSupervisedWorkerOptions
{
    public required string DotNetHostPath { get; init; }
    public required string WorkerAssemblyPath { get; init; }
    public FeedDeploymentProfile DeploymentProfile { get; init; } = FeedDeploymentProfile.Development;
    public FeedDataSourceMode DataSource { get; init; } = FeedDataSourceMode.Synthetic;
    public SyntheticFeedOptions Synthetic { get; init; } = new();

    public DatabentoSupervisedWorkerOptions Validate()
    {
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
    TimeProvider timeProvider) : IDatabentoLifecycleRuntime
{
    DateOnly? activeValueDate;
    public DateOnly? ActiveValueDate => activeValueDate;

    public async Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken)
        => _ = await contractAuthority.ReconcileAsync(valueDate,
            nameof(SupervisedDatabentoLifecycleRuntime), cancellationToken).ConfigureAwait(false);

    public async Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken)
    {
        if (activeValueDate is not null)
            throw new InvalidOperationException("Supervised market data is already active.");
        var launch = workerOptions.Validate();
        var grouped = registrations.Snapshot()
            .GroupBy(value => value.Dataset, StringComparer.Ordinal)
            .ToArray();
        if (grouped.Length == 0)
            throw new InvalidOperationException("No dataset contract manifest is available for Stage 3 startup.");
        try
        {
            foreach (var dataset in grouped)
            {
                var contracts = dataset.Select(value => value.DomainContractId)
                    .Distinct(StringComparer.Ordinal).ToArray();
                await workers.StartOwnedAsync(new DatasetWorkerStartRequest
                {
                    ExecutablePath = launch.DotNetHostPath,
                    PrefixArguments = [launch.WorkerAssemblyPath,
                        "--contracts", string.Join('|', contracts),
                        "--deployment-profile", launch.DeploymentProfile.ToString(),
                        "--data-source", launch.DataSource.ToString(),
                        "--synthetic-record-count", launch.Synthetic.RecordCount.ToString(),
                        "--synthetic-records-per-second", launch.Synthetic.RecordsPerSecond.ToString(),
                        "--synthetic-start-sequence", launch.Synthetic.StartSequence.ToString()],
                    Dataset = dataset.Key,
                    ValueDate = valueDate,
                    WorkerInstanceId = Guid.NewGuid(),
                    GenerationId = Guid.NewGuid(),
                    ManifestRevision = 1
                }, cancellationToken).ConfigureAwait(false);
            }
            activeValueDate = valueDate;
        }
        catch
        {
            await workers.StopAllAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await workers.StopAllAsync(cancellationToken).ConfigureAwait(false);
        activeValueDate = null;
    }

    public Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request, CancellationToken cancellationToken) =>
        workers.ResetOwnedAsync(request, cancellationToken);

    public async ValueTask<DatabentoBulkWatchdogSnapshot> GetWatchdogSnapshotAsync(
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contractSnapshot = registrations.Snapshot();
        var snapshots = await workers.GetHealthAsync(timeout, cancellationToken).ConfigureAwait(false);
        var feeds = snapshots.Select((worker, index) =>
        {
            var contracts = contractSnapshot.Where(value => string.Equals(
                value.Dataset, worker.Dataset, StringComparison.Ordinal)).ToArray();
            var healthy = worker.Running && worker.Healthy;
            return new DatabentoFeedWatchdogStatus
            {
                FeedInstanceId = checked((ulong)(index + 1)),
                GenerationId = worker.GenerationId,
                Dataset = worker.Dataset,
                FeedKind = "Ticker",
                Criticality = contracts.Any(IsCore)
                    ? DatabentoFeedCriticality.Core : DatabentoFeedCriticality.Optional,
                MajorStatus = healthy ? DatabentoMajorStatus.Up : DatabentoMajorStatus.Down,
                NativeState = worker.Running ? "WorkerRunning" : "WorkerExited",
                TerminalStatus = worker.Running ? 0 : worker.ExitCode ?? -1,
                ProducerAlive = worker.Running,
                AggregationWorkerRunning = worker.Healthy,
                TransportRunning = worker.Running,
                ExpectedSubscriptions = contracts.Length,
                ReceivedSubscriptions = healthy ? contracts.Length : 0,
                HeartbeatCount = healthy ? 1UL : 0,
                ProviderMessageCount = 0,
                LastHeartbeatAge = healthy ? TimeSpan.Zero : TimeSpan.MaxValue,
                LastProviderMessageAge = TimeSpan.Zero,
                RecordsProduced = 0,
                RecordsConsumed = 0,
                RingCapacity = 0,
                RingUsed = 0,
                RingHighWater = 0,
                RingOverruns = 0,
                FailureDetail = healthy ? string.Empty : worker.Detail,
                ContractRoles = contracts.Select(Role).Where(value => value.HasValue)
                    .Select(value => value!.Value).Distinct().ToArray(),
                ContractIds = contracts.Select(value => value.DomainContractId).ToArray()
            };
        }).ToArray();
        var complete = activeValueDate is not null && feeds.Length != 0
            && feeds.All(feed => feed.MajorStatus == DatabentoMajorStatus.Up);
        return new DatabentoBulkWatchdogSnapshot
        {
            Complete = complete,
            NativeBackend = workerOptions.DataSource == FeedDataSourceMode.Synthetic
                ? "SupervisedSynthetic" : "SupervisedDatabentoLive",
            NativeAbiVersion = 3,
            NativeGeneration = feeds.FirstOrDefault()?.GenerationId ?? Guid.Empty,
            ObservedOnUtc = timeProvider.GetUtcNow().UtcDateTime,
            Feeds = feeds,
            FailureDetail = complete ? string.Empty : "One or more supervised dataset workers are not qualified."
        };
    }

    static bool IsCore(DatabentoContractRegistration value) => Role(value).HasValue;
    static DatabentoContractRole? Role(DatabentoContractRegistration value)
    {
        if (!value.Rollover) return null;
        if (string.Equals(value.RootSymbol, "ES", StringComparison.OrdinalIgnoreCase))
            return DatabentoContractRole.EsQuarterly;
        if (!string.Equals(value.RootSymbol, "VX", StringComparison.OrdinalIgnoreCase)) return null;
        return value.OnTheRun ? DatabentoContractRole.VxFrontMonth : DatabentoContractRole.VxSecondMonth;
    }
}
