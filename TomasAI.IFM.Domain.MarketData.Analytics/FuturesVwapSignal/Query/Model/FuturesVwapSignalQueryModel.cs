using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Model;

/// <summary>Executes storage-backed VWAP queries.</summary>
internal static class FuturesVwapSignalQueryModel
{
    /// <summary>Loads the latest projected session VWAP.</summary>
    internal static ValueTask<FuturesVwapSignalReadModel?> ExecuteAsync(
        GetLatestFuturesVwapSignalQuery query,
        IDbContextFactory dbFactory, CancellationToken cancellationToken) =>
        new(dbFactory.MarketDataDb.GetLatestFuturesVwapSignalAsync(
            query.ContractId, query.ValueDate, query.ConfigurationId, cancellationToken));

    /// <summary>Loads projected updates for one session.</summary>
    internal static async ValueTask<FuturesVwapSignalReadModel[]> ExecuteAsync(
        GetFuturesVwapSignalHistoryQuery query,
        IDbContextFactory dbFactory, CancellationToken cancellationToken) =>
        (await dbFactory.MarketDataDb.GetFuturesVwapSignalHistoryAsync(
            query.ContractId, query.ValueDate, query.ConfigurationId, cancellationToken)
            .ConfigureAwait(false)).ToArray();
}
