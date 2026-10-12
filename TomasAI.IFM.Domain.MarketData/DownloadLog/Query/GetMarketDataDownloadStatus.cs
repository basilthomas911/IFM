using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query;

public static class GetMarketDataDownloadStatus
{
    public static Task<MarketDataDownloadStatusResult> ExecuteAsync(this GetMarketDataDownloadStatusQuery query, IDbContextFactory db, CancellationToken cancellationToken)
        
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(query);
        return db.MarketDataDb.GetMarketDataDownloadStatusAsync(query.Request, query.RequiredImportCommandId, query.Cursor, cancellationToken);
    }
}
