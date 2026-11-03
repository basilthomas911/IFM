using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetLastVixFuturesEodData
{

    extension(GetLastVixFuturesEodDataQuery q)
    {
        internal async ValueTask<VixFuturesEodDataReadModel?> ExecuteAsync(
        IDbContextFactory dbFactory)
            => await (dbFactory.MarketDataDb.GetLastVixFuturesEodDataAsync(q.ContractId, q.ValueDate)).ConfigureAwait(false);
    }
}
