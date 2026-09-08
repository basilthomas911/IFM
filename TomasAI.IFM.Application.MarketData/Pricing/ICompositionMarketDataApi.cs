namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Qualified composition operations routed through the supervised dataset owner.</summary>
public interface ICompositionMarketDataApi
{
    Task<WorkerOptionChainResult> AcquireAsync(string dataset, WorkerOptionChainRequest request, CancellationToken cancellationToken);
    Task<WorkerOptionChainResult> ReleaseAsync(string dataset, WorkerOptionChainRelease request, CancellationToken cancellationToken);
    Task<CompositionSnapshotResult> CaptureAsync(string dataset, CompositionSnapshotRequest request, CancellationToken cancellationToken);
}
