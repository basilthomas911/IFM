using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Defines the provider-neutral application historical API used by domain data load actors.</summary>
public interface IMarketDataHistoricalApi
{
    /// <summary>Returns the exclusive schema availability end, or null when unsupported.</summary>
    ValueTask<DateTimeOffset?> GetAvailableEndAsync(MarketDataHistoricalRequest request, CancellationToken cancellationToken)
        => ValueTask.FromResult<DateTimeOffset?>(null);

    /// <summary>Estimates a request without submitting billable provider work.</summary>
    ValueTask<MarketDataHistoricalEstimate> EstimateAsync(
        MarketDataHistoricalRequest request,
        CancellationToken cancellationToken);

    /// <summary>Acquires or resumes a request and streams bounded normalized batches to a durable sink.</summary>
    ValueTask<MarketDataHistoricalManifest> AcquireAsync(
        MarketDataHistoricalRequest request,
        HistoricalAcquisitionCheckpoint checkpoint,
        IHistoricalObservationSink sink,
        CancellationToken cancellationToken);
}
