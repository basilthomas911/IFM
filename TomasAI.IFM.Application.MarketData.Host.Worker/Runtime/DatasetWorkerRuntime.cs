using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Host.Worker;

/// <summary>A native/managed dataset generation owned entirely by the worker process.</summary>
internal sealed class DatasetWorkerRuntime : IAsyncDisposable
{
    readonly IDatabentoMarketDataEpoch epoch;
    readonly DatasetSubscriptionManifest manifest;
    readonly DatabentoMarketDataApi api;

    DatasetWorkerRuntime(IDatabentoMarketDataEpoch epoch, DatasetSubscriptionManifest manifest, DatabentoMarketDataApi api, long diagnosticBaseline)
    {
        this.diagnosticBaseline = diagnosticBaseline;
        this.epoch = epoch;
        this.api = api;
        this.manifest = manifest;
        GenerationId = epoch.GetHealth().DatasetFeedStatuses?.Single().GenerationId ?? Guid.Empty;
    }

    public Guid GenerationId { get; }
    public Task<byte[]> InvokeMarketDataApiAsync(MarketData.Remote.MarketDataApiCall request, CancellationToken token)
        => new MarketData.Remote.MarketDataApiDispatcher(api, allowLifecycle: false).InvokeAsync(request, token);
    public Task<MarketData.Pricing.WorkerOptionChainResult> AcquireOptionChainAsync(MarketData.Pricing.WorkerOptionChainRequest request, CancellationToken token)
        => epoch.AcquireOptionChainAsync(request, token);
    public Task<MarketData.Pricing.WorkerOptionChainResult> ReleaseOptionChainAsync(MarketData.Pricing.WorkerOptionChainRelease request, CancellationToken token)
        => epoch.ReleaseOptionChainAsync(request, token);
    public Task<MarketData.Pricing.CompositionSnapshotResult> CaptureCompositionSnapshotAsync(MarketData.Pricing.CompositionSnapshotRequest request, CancellationToken token)
        => epoch.CaptureCompositionSnapshotAsync(request, token);
    string? processingFailure;
    readonly long diagnosticBaseline;
    public void MarkFailed(Exception error)
    {
        if (Interlocked.CompareExchange(ref processingFailure, DatasetWorkerLogging.Redact(error.ToString()), null) is null)
            Serilog.Log.Error("Dataset processing exception; Dataset={Dataset}; GenerationId={GenerationId}; Detail={Detail}",
                manifest.Dataset, GenerationId, DatasetWorkerLogging.Redact(error.ToString()));
    }
    public bool IsHealthy
    {
        get
        {
            try { return processingFailure is null && DatabentoDiagnostics.ErrorCount == diagnosticBaseline
                && epoch.GetHealth() is { ProcessingFailures: 0, PublicationFailures: 0 } && epoch.IsFeedUp(TimeSpan.FromSeconds(1)); }
            catch (Exception error) { MarkFailed(error); return false; }
        }
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Optional runtime diagnostic capture returns unhealthy diagnostics when sampling fails.")]
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
            MarkFailed(exception);
            return DatasetWorkerDiagnostics.Unavailable(manifest.Dataset, GenerationId,
                $"Dataset diagnostics failed: {exception.Message}", observedOnUtc);
        }
    }
    public string Detail
    {
        get
        {
            var health = epoch.GetHealth();
            return $"failure={processingFailure ?? DatabentoDiagnostics.LastFailure}; running={health.Running}; aggregation={health.AggregationRunning}; "
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
        CancellationToken cancellationToken, Pricing.OptionPricingRefreshPolicy? optionPricingRefresh = null, Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null)
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
            OptionPricingRefresh = optionPricingRefresh ?? new(),
            Contracts = manifest.GetRegistrations()
        };
        var factory = new DatabentoMarketDataEpochFactory(
            new DatabentoFeedFactory(), publisher, options, loggerFactory: loggerFactory, tradeEvidence: publisher as Pricing.IOptionTradeEvidenceWriter);
        var diagnosticBaseline = DatabentoDiagnostics.ErrorCount;
        var newEpoch = factory.Create(manifest.ValueDate);
        try
        {
            var api = new DatabentoMarketDataApi(new OwnedEpochFactory(newEpoch), new());
            await api.StartAsync(manifest.ValueDate, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new(newEpoch, manifest, api, diagnosticBaseline);
        }
        catch
        {
            await newEpoch.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await api.DisposeAsync().ConfigureAwait(false);
    sealed class OwnedEpochFactory(IDatabentoMarketDataEpoch epoch) : IDatabentoMarketDataEpochFactory
    { public IDatabentoMarketDataEpoch Create(DateOnly valueDate) => epoch; }

}
