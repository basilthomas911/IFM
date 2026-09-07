using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Extensions;
public static class GetMarketDataDownloadHistory
{
    public static Task<MarketDataDownloadHistoryResult> ExecuteAsync(this GetMarketDataDownloadHistoryQuery query, IDbContextFactory db, CancellationToken cancellationToken)
        => db.MarketDataDb.GetMarketDataDownloadHistoryAsync(query.Request, query.PageSize, query.Cursor, cancellationToken);
}
