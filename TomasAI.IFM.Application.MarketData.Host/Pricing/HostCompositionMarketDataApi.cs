using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Routes backend pricing operations only to the independently owned option dataset.</summary>
public sealed class HostCompositionMarketDataApi(MarketDataWorkerHost host) : ICompositionMarketDataApi
{
    Guid Owner(string dataset, Guid generation) => host.GetBackendStatus().Datasets.Single(state => state.Role == "Options"
        && state.Dataset == dataset && state.GenerationId == generation && state.Lifecycle == "Healthy").WorkerInstanceId;
    public Task<WorkerOptionChainResult> AcquireAsync(string dataset, WorkerOptionChainRequest request, CancellationToken token)
        => host.AcquireOptionLeaseAsync("Options", Owner(dataset, request.GenerationId), request, token);
    public Task<WorkerOptionChainResult> ReleaseAsync(string dataset, WorkerOptionChainRelease request, CancellationToken token)
        => host.ReleaseOptionLeaseAsync("Options", Owner(dataset, request.GenerationId), request, token);
    public Task<CompositionSnapshotResult> CaptureAsync(string dataset, CompositionSnapshotRequest request, CancellationToken token)
        => host.RequireWorker("Options", Owner(dataset, request.GenerationId)).CaptureCompositionSnapshotAsync(request, token);
}
