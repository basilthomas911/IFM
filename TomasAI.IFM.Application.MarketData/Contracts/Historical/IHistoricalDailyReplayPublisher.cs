using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Publishes ordered, completed Daily observations to bar-derived Analytics actors.</summary>
public interface IHistoricalDailyReplayPublisher
{
    /// <summary>Restores a compatible committed checkpoint before provider access; null means stored observations are needed.</summary>
    ValueTask<DateOnly?> RestoreAsync(MarketSeriesIdentity series, DateOnly targetValueDate,
        string targetContractId, DateOnly lastCompletedDate, CancellationToken cancellationToken)
        => ValueTask.FromResult<DateOnly?>(null);

    /// <summary>Publishes a complete ordered replay window.</summary>
    ValueTask PublishAsync(
        IReadOnlyList<FuturesEodObservationReadModel> observations,
        DateOnly targetValueDate,
        string targetContractId,
        CancellationToken cancellationToken);
}
