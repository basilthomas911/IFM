using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

/// <summary>Handles complete historical Futures ITI timeframe reads.</summary>
public static class GetFuturesItiSignalHistory
{
    /// <summary>Returns all durable signals in chronological order for the requested timeframe.</summary>
    internal static async ValueTask<FuturesItiSignalV2ReadModel[]> GetFuturesItiSignalHistoryAsync(
        this GetFuturesItiSignalHistoryQuery query,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = FuturesItiSignalHistoryWindow.Resolve(query.ValueDate, query.TimePeriod);
        var rows = await dbFactory.MarketDataDb.GetFuturesItiSignalsForContractAsync(
            query.ContractId,
            window.StartValueDate,
            window.EndValueDate).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return [.. rows
            .Where(row => row.TimePeriod == query.TimePeriod)
            .OrderBy(static row => row.IntrinsicTime)
            .ThenBy(static row => row.SequenceId)];
    }
}
