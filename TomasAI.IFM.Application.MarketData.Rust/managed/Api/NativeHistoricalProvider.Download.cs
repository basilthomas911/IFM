using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;
public sealed partial class NativeHistoricalProvider
{
    /// <summary>Streams into a provisional sibling file, verifies complete native
    /// and storage checksums/size, then atomically replaces the requested path.
    /// Failure preserves an existing destination and removes provisional output.</summary>
    public async ValueTask DownloadBatchFileAsync(string providerJobId,HistoricalProviderFile file,string destinationPath,CancellationToken cancellationToken)
        =>await runtime.DownloadHistoricalFileAsync(providerJobId,file,destinationPath,budget,cancellationToken).ConfigureAwait(false);
}
