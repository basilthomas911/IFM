using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodData
{

    extension(GetFuturesEodDataQuery q)
    {
        internal async ValueTask<FuturesEodDataV2ReadModel> ExecuteAsync(
        IDbContextFactory dbFactory)
        {
            var db = dbFactory.MarketDataDb;
            var futuresEodData = await (db.GetFuturesEodDataAsync(q.ContractId, q.ValueDate)).ConfigureAwait(false);
            if (futuresEodData is not null)
            {
                var movingAverages = await (dbFactory.GetFuturesEodMovingAveragesAsync(q.ContractId, futuresEodData.GetContractId().Symbol, q.ValueDate)).ConfigureAwait(false);
                futuresEodData = futuresEodData with { FiftyDMA = movingAverages.FiftyDMA, TwoHundredDMA = movingAverages.TwoHundredDMA };
            }
            return futuresEodData!;
        }
    }
}
