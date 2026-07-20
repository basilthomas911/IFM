using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataParameters
{
    internal static async ValueTask GetFuturesEodDataParametersAsync(
       this GetFuturesEodDataParametersQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await GetFuturesEodDataParametersAsync(dbFactory.MarketDataDb, q.ContractId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataQuery.Verb, new ServiceResult<FuturesEodDataParametersReadModel>(result));

        async ValueTask<FuturesEodDataParametersReadModel> GetFuturesEodDataParametersAsync(
            IMarketDataDbContext db, string contractId, DateOnly valueDate)
            => new (
                FuturesEodDataToday: await db.GetFuturesEodDataAsync(contractId, valueDate),
                FuturesEodDataRange: [.. await db.GetFuturesEodDataByDateRangeAsync(contractId, valueDate.AddMonths(-2), valueDate.AddDays(-1))],
                NormalCurveTable: await db.GetNormalCurveTableAsync());
    }
}
