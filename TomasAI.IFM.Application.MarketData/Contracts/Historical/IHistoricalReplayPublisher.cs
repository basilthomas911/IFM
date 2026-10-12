using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Publishes bounded private replay batches to analytics realtime workers.</summary>
public interface IHistoricalReplayPublisher
{
    /// <summary>Publishes one already-normalized bounded batch.</summary>
    ValueTask PublishAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken);
}
