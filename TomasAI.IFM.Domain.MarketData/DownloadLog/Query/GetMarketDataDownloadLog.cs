using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query;

public static class GetMarketDataDownloadLog
{

    extension(GetMarketDataDownloadLogQuery query)
    {
        public Task<MarketDataDownloadLogResult> ExecuteAsync(IDbContextFactory db, CancellationToken cancellationToken)

        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(query.Attempt);
            MarketDataDownloadReadRequests.ValidateSelection(query.Request, 1, query.Attempt);
            return db.MarketDataDb.GetMarketDataDownloadLogAsync(query.Request, query.Attempt, cancellationToken);
        }
    }
}
