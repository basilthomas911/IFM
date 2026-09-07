using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Extensions;
public static class GetMarketDataDownloadStatus
{
    public static Task<MarketDataDownloadStatusResult> ExecuteAsync(this GetMarketDataDownloadStatusQuery query, IDbContextFactory db, CancellationToken cancellationToken)
        => db.MarketDataDb.GetMarketDataDownloadStatusAsync(query.Request, query.RequiredImportCommandId, query.Cursor, cancellationToken);
}
