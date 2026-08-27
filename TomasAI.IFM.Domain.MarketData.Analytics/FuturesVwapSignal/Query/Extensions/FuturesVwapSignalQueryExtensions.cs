using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Extensions;

/// <summary>Executes storage-backed VWAP queries.</summary>
public static class FuturesVwapSignalQueryExtensions
{
    /// <summary>Loads the latest projected session VWAP.</summary>
    public static ValueTask<FuturesVwapSignalReadModel?> ExecuteAsync(
        this GetLatestFuturesVwapSignalQuery query,
        IDbContextFactory dbFactory, CancellationToken cancellationToken) =>
        new(dbFactory.MarketDataDb.GetLatestFuturesVwapSignalAsync(
            query.ContractId, query.ValueDate, query.ConfigurationId, cancellationToken));

    /// <summary>Loads projected updates for one session.</summary>
    public static async ValueTask<FuturesVwapSignalReadModel[]> ExecuteAsync(
        this GetFuturesVwapSignalHistoryQuery query,
        IDbContextFactory dbFactory, CancellationToken cancellationToken) =>
        (await dbFactory.MarketDataDb.GetFuturesVwapSignalHistoryAsync(
            query.ContractId, query.ValueDate, query.ConfigurationId, cancellationToken)
            .ConfigureAwait(false)).ToArray();
}
