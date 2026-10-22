using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Controls one worker lifetime locally or through the independent MarketData Host.</summary>
public interface IDatasetWorkerProcess : IAsyncDisposable
{
    event Action<DatasetWorkerProcessSnapshot>? Exited;
    DatasetWorkerProcessSnapshot Current { get; }
    Task<DatasetWorkerProcessSnapshot> StartAsync(DatasetWorkerStartRequest request, CancellationToken cancellationToken = default);
    Task<DatasetWorkerControlFrame> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<DatasetWorkerControlFrame> ResetAsync(DatasetSubscriptionManifest manifest, CancellationToken cancellationToken = default);
    Task<DatasetWorkerControlFrame> ApplyManifestAsync(DatasetSubscriptionManifest manifest, CancellationToken cancellationToken = default);
    Task<DatasetWorkerProcessSnapshot> StopAsync(CancellationToken cancellationToken = default);
    Task TerminateAsync();
    Task<byte[]> InvokeMarketDataApiAsync(Remote.MarketDataApiCall request, CancellationToken cancellationToken);
    Task<WorkerOptionChainResult> AcquireOptionChainAsync(WorkerOptionChainRequest request, CancellationToken cancellationToken);
    Task<WorkerOptionChainResult> ReleaseOptionChainAsync(WorkerOptionChainRelease request, CancellationToken cancellationToken);
    Task<WorkerOptionChainResult> ApplyOptionChainOwnershipAsync(WorkerOptionChainRelease request, CancellationToken cancellationToken);
    Task<CompositionSnapshotResult> CaptureCompositionSnapshotAsync(CompositionSnapshotRequest request, CancellationToken cancellationToken);
}

/// <summary>Starts a fixed worker role; no executable paths or secrets cross this contract.</summary>
public sealed record MarketDataWorkerStart(DatasetSubscriptionManifest Manifest, Guid WorkerInstanceId, Guid GenerationId);
