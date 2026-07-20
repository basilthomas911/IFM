using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetLastFuturesEodData
{
    internal static async ValueTask GetLastFuturesEodDataAsync(
       this GetLastFuturesEodDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var db = dbFactory.MarketDataDb;
        var futuresEodData = await db.GetLastFuturesEodDataAsync(q.ContractId, q.ValueDate);
        if (futuresEodData is not null)
        {
            var contractIdInfo = futuresEodData.GetContractId();
            var movingAverages = await dbFactory.GetFuturesEodMovingAveragesAsync(contractIdInfo.ContractId, contractIdInfo.Symbol, futuresEodData.ValueDate);
            futuresEodData = futuresEodData with { FiftyDMA = movingAverages.FiftyDMA, TwoHundredDMA = movingAverages.TwoHundredDMA };
        }
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataQuery.Verb, new ServiceResult<FuturesEodDataV2ReadModel>(futuresEodData!));
    }

}
