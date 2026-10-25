using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Historical;

/// <summary>Streams bounded decoded records from a provider range or verified file.</summary>
public interface IHistoricalRecordReader : IAsyncDisposable
{
    /// <summary>Reads the next bounded batch, or <see langword="null"/> at end of input.</summary>
    ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken);
}
