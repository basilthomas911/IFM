using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Qualified application composition over the current native epoch.
/// Dataset and logical owner checks prevent a stale lease/capture from crossing
/// into a replacement epoch. No worker process or legacy provider is resolved.</summary>
public sealed class NativeCompositionMarketDataApi(NativeMarketDataEpochFactory epochs):ICompositionMarketDataApi
{
    NativeMarketDataEpoch Owner(string dataset)
    {
        var epoch=epochs.Current;
        if(dataset!="GLBX.MDP3"&&dataset!="XCBF.PITCH")throw new ArgumentException("Unsupported qualified dataset.",nameof(dataset));
        return epoch;
    }
    /// <inheritdoc/>
    public Task<WorkerOptionChainResult> AcquireAsync(string dataset,WorkerOptionChainRequest request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();var epoch=Owner(dataset);
        // Generation replacement is an expected compare-and-swap miss, not an
        // unknown processing exception. Keep the request fenced and let normal
        // discovery return its typed Recovering result without condemning the
        // replacement owner through the background exception-health reporter.
        if(dataset!="GLBX.MDP3"||request.GenerationId!=epoch.OptionGeneration)
            return Task.FromResult(new WorkerOptionChainResult(false,new("Recovering","GenerationId","","The native option owner has changed.")));
        if(epoch.ValueDate!=request.ValueDate)throw new InvalidOperationException("Option lease ValueDate differs from the API epoch.");
        return epoch.AcquireOptionChainAsync(request,cancellationToken);
    }
    /// <inheritdoc/>
    public Task<WorkerOptionChainResult> ReleaseAsync(string dataset,WorkerOptionChainRelease request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(dataset!="GLBX.MDP3")throw new ArgumentException("Unsupported qualified options dataset.",nameof(dataset));
        if(!epochs.TryGetCurrent(out var epoch))return Task.FromResult(new WorkerOptionChainResult(false,null));
        // Discovery leases represent logical intent retained across replacement.
        // Releasing an exact lease GUID must retire its restored binding after
        // transport rotation. Revision-based business authority, acquisitions
        // and reads retain strict generation qualification.
        if(request.GenerationId!=epoch!.OptionGeneration&&request.LeaseId!=Guid.Empty&&request.Ownership is null)
            request=request with{GenerationId=epoch.OptionGeneration};
        return epoch.ReleaseOptionChainAsync(request,cancellationToken);
    }
    /// <inheritdoc/>
    public Task<CompositionSnapshotResult> CaptureAsync(string dataset,CompositionSnapshotRequest request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();var epoch=Owner(dataset);
        if(request.IncludeOptions&&(dataset!="GLBX.MDP3"||request.GenerationId!=epoch.OptionGeneration))
            return Task.FromResult(new CompositionSnapshotResult(null,new("Recovering","GenerationId","","The native option owner has changed.")));
        return epoch.CaptureCompositionSnapshotAsync(request,cancellationToken);
    }
}
