using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;
public sealed partial class NativeHistoricalProvider
{
    /// <summary>Opens a lossless bounded native range reader. Network decoding and
    /// consumer backpressure share the original query deadline. Rows are provisional
    /// until the final batch/completion succeeds; no full-range buffer is allocated.</summary>
    public ValueTask<IHistoricalRecordReader> OpenRangeAsync(HistoricalProviderRequest request,int maximumBatchRecords,CancellationToken cancellationToken)
        {ValidateBatchSize(maximumBatchRecords);return ValueTask.FromResult(runtime.OpenHistoricalRange(request,maximumBatchRecords,budget,cancellationToken));}
    void ValidateBatchSize(int value){if(value<1||value>maximumBatchRecords)throw new ArgumentOutOfRangeException(nameof(value));}
    /// <summary>Feeds a validated storage path through fixed 65,536-byte input
    /// transfers to the native bounded DBN/DBZ decoder. No Rust filesystem task runs.</summary>
    public ValueTask<IHistoricalRecordReader> OpenFileAsync(string path,HistoricalDataSchema schema,int maximumBatchRecords,CancellationToken cancellationToken)
    {cancellationToken.ThrowIfCancellationRequested();ValidateBatchSize(maximumBatchRecords);if(!File.Exists(path))throw new FileNotFoundException("Historical file was not found.",path);return ValueTask.FromResult(runtime.OpenHistoricalFile(Path.GetFullPath(path),schema,maximumBatchRecords,budget,cancellationToken));}
}
