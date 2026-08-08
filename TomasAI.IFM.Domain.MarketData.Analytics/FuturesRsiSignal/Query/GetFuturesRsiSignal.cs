using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;

public static class GetFuturesRsiSignal
{
    /// <summary>
    /// Handles a request to retrieve the most recent RSI signal for a specified contract, value date, and signal type.
    /// </summary>
    /// <param name="q">The query containing contract identifier, value date, and signal type filters.</param>
    /// <param name="dbFactory">The database context factory used to access futures RSI signal data.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    public static async ValueTask<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(
        this GetFuturesRsiSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength).ConfigureAwait(false);

    public static async ValueTask<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionAsync(
        this GetFuturesTrendDirectionFromRSISignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                q.ContractId,
                q.ValueDate,
                q.TimePeriod,
                q.PeriodLength,
                q.Timestamp,
                q.LookBackInterval,
                q.StartTime,
                q.EndTime,
                cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                q.ContractId,
                q.ValueDate,
                q.TimePeriod,
                q.PeriodLength,
                q.Timestamp,
                q.LookBackInterval,
                q.StartTime,
                q.EndTime).ConfigureAwait(false);
}
