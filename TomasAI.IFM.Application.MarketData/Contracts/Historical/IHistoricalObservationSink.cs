using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Accepts bounded normalized batches and returns only after durable checkpointing.</summary>
public interface IHistoricalObservationSink
{
    /// <summary>Persists one bounded batch idempotently.</summary>
    ValueTask AcceptAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken);
}
