using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataParameters
{
    internal static async ValueTask<FuturesEodDataParametersReadModel> GetFuturesEodDataParametersAsync(
       this GetFuturesEodDataParametersQuery q, IDbContextFactory dbFactory)
    {
        return await GetFuturesEodDataParametersAsync(dbFactory.MarketDataDb, q.ContractId, q.ValueDate);

        async ValueTask<FuturesEodDataParametersReadModel> GetFuturesEodDataParametersAsync(
            IMarketDataDbContext db, string contractId, DateOnly valueDate)
            => new (
                FuturesEodDataToday: await db.GetFuturesEodDataAsync(contractId, valueDate),
                FuturesEodDataRange: [.. await db.GetFuturesEodDataByDateRangeAsync(contractId, valueDate.AddMonths(-2), valueDate.AddDays(-1))],
                NormalCurveTable: await db.GetNormalCurveTableAsync());
    }
}
