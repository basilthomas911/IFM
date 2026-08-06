using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;

public static class GetLastFuturesAdxSignal
{
    /// <summary>
    /// Handles a <see cref="GetFuturesAdxSignalQuery"/> by retrieving the most recent ADX signal
    /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
    /// </summary>
    public static async ValueTask<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(
        this GetFuturesAdxSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength).ConfigureAwait(false);
}
