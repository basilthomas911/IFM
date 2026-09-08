using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Worker;

/// <summary>A native/managed dataset generation owned entirely by the worker process.</summary>
internal sealed class DatasetWorkerRuntime : IAsyncDisposable
{
    readonly IDatabentoMarketDataEpoch epoch;
    readonly DatasetSubscriptionManifest manifest;

    DatasetWorkerRuntime(IDatabentoMarketDataEpoch epoch, DatasetSubscriptionManifest manifest)
    {
        this.epoch = epoch;
        this.manifest = manifest;
        GenerationId = epoch.GetHealth().DatasetFeedStatuses?.Single().GenerationId ?? Guid.Empty;
    }

    public Guid GenerationId { get; }
    public Task<MarketData.Pricing.WorkerOptionChainResult> AcquireOptionChainAsync(MarketData.Pricing.WorkerOptionChainRequest request, CancellationToken token)
        => epoch.AcquireOptionChainAsync(request, token);
    public Task<MarketData.Pricing.WorkerOptionChainResult> ReleaseOptionChainAsync(MarketData.Pricing.WorkerOptionChainRelease request, CancellationToken token)
        => epoch.ReleaseOptionChainAsync(request, token);
    public Task<MarketData.Pricing.CompositionSnapshotResult> CaptureCompositionSnapshotAsync(MarketData.Pricing.CompositionSnapshotRequest request, CancellationToken token)
        => epoch.CaptureCompositionSnapshotAsync(request, token);
    public bool IsHealthy => epoch.IsFeedUp(TimeSpan.FromSeconds(1));
    public DatasetWorkerDiagnostics GetDiagnostics()
    {
        var observedOnUtc = DateTime.UtcNow;
        try
        {
            var health = epoch.GetHealth();
            var available = DatabentoNativeWatchdog.TryRead(out var native, out var failure);
            var diagnostics = DatasetWorkerDiagnostics.Capture(manifest, health,
                available ? native : null, failure, observedOnUtc);
            // Validate within the diagnostic boundary. An inconsistent observation must be
            // reported as unavailable, not throw again while constructing a failure reply.
            diagnostics.Validate();
            return diagnostics;
        }
        catch (Exception exception)
        {
            return DatasetWorkerDiagnostics.Unavailable(manifest.Dataset, GenerationId,
                $"Dataset diagnostics failed: {exception.Message}", observedOnUtc);
        }
    }
    public string Detail
    {
        get
        {
            var health = epoch.GetHealth();
            return $"running={health.Running}; aggregation={health.AggregationRunning}; "
                + $"contracts={health.ConfiguredContracts}; trades={health.SourceTradeRecords}; "
                + $"quotes={health.SourceQuoteRecords}; failures={health.ProcessingFailures + health.PublicationFailures}";
        }
    }

    public static async Task<DatasetWorkerRuntime> StartAsync(
        DatasetSubscriptionManifest manifest,
        FeedDeploymentProfile deploymentProfile,
        FeedDataSourceMode dataSource,
        SyntheticFeedOptions synthetic,
        ITickAggregationEventPublisher publisher,
        CancellationToken cancellationToken)
    {
        manifest.Validate();
        var feedOptions = DatabentoFeedOptions.ForProfile(deploymentProfile, manifest.Dataset) with
        {
            DataSource = dataSource,
            Synthetic = synthetic
        };
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = feedOptions,
            Contracts = manifest.GetRegistrations()
        };
        var factory = new DatabentoMarketDataEpochFactory(
            new DatabentoFeedFactory(), publisher, options);
        var epoch = factory.Create(manifest.ValueDate);
        try
        {
            await epoch.StartAsync(cancellationToken).ConfigureAwait(false);
            return new(epoch, manifest);
        }
        catch
        {
            await epoch.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await epoch.DisposeAsync().ConfigureAwait(false);

}
