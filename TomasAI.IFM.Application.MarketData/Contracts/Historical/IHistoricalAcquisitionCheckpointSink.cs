using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Accepts acquisition-stage checkpoints before normalized record batches are available.</summary>
public interface IHistoricalAcquisitionCheckpointSink
{
    /// <summary>Persists one monotonic provider acquisition checkpoint.</summary>
    ValueTask CheckpointAsync(
        HistoricalAcquisitionCheckpoint checkpoint,
        CancellationToken cancellationToken);
}
