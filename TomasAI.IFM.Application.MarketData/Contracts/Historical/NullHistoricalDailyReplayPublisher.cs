using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Provides a no-op Daily replay target outside Analytics hosts.</summary>
public sealed class NullHistoricalDailyReplayPublisher : IHistoricalDailyReplayPublisher
{
    /// <inheritdoc />
    public ValueTask PublishAsync(
        IReadOnlyList<FuturesEodObservationReadModel> observations,
        DateOnly targetValueDate,
        string targetContractId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observations);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
