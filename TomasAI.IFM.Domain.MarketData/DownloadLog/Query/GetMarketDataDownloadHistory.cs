using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query;

public static class GetMarketDataDownloadHistory
{

    extension(GetMarketDataDownloadHistoryQuery query)
    {
        public Task<MarketDataDownloadHistoryResult> ExecuteAsync(IDbContextFactory db, CancellationToken cancellationToken)

        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(query);
            MarketDataDownloadReadRequests.ValidateSelection(query.Request, query.PageSize, query.Cursor);
            return db.MarketDataDb.GetMarketDataDownloadHistoryAsync(query.Request, query.PageSize, query.Cursor, cancellationToken);
        }
    }
}
