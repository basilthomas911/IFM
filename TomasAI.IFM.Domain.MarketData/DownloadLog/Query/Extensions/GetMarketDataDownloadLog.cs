using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Extensions;
public static class GetMarketDataDownloadLog
{
    public static Task<MarketDataDownloadLogResult> ExecuteAsync(this GetMarketDataDownloadLogQuery query, IDbContextFactory db, CancellationToken cancellationToken)
        => db.MarketDataDb.GetMarketDataDownloadLogAsync(query.Request, query.Attempt, cancellationToken);
}
