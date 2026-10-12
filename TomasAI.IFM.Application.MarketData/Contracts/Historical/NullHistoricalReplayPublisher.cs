using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Provides a no-op replay target for raw-data-load-only processes.</summary>
public sealed class NullHistoricalReplayPublisher : IHistoricalReplayPublisher
{
    /// <inheritdoc />
    public ValueTask PublishAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
