using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

public static class GetFuturesItiTrendDirectionChangedSignals
{
    /// <summary>
    /// Handles the GetFuturesItiTrendDirectionChangedSignalsQuery by retrieving the relevant Futures ITI trend direction changed signals from the database and replying with the results.
    /// </summary>
    /// <param name="q">The query for retrieving trend direction changed signals.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FuturesItiSignalV2ReadModel[]> GetFuturesItiTrendDirectionChangedSignalsAsync(
        this GetFuturesItiTrendDirectionChangedSignalsQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => [.. cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb
                .GetFuturesItiTrendDirectionChangedSignalsAsync(q.ContractId, q.ValueDate, cancellationToken)
                .ConfigureAwait(false)
            : await dbFactory.MarketDataDb
                .GetFuturesItiTrendDirectionChangedSignalsAsync(q.ContractId, q.ValueDate)
                .ConfigureAwait(false)];
}
