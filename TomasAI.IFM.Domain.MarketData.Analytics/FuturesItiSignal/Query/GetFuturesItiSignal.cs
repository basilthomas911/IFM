using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

public static class GetFuturesItiSignal
{
    /// <summary>
    /// Handles <see cref="GetFuturesItiSignalQuery"/> by retrieving the latest ITI signal snapshot for the
    /// requested contract and value date, then replying to the caller.
    /// </summary>
    internal static async ValueTask<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(
        this GetFuturesItiSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(
                q.ContractId, q.ValueDate, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(
                q.ContractId, q.ValueDate).ConfigureAwait(false);
}
