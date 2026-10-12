using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Defines the provider-neutral application historical API used by domain data load actors.</summary>
public interface IMarketDataHistoricalApi
{
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
