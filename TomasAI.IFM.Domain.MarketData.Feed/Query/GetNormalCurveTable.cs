using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetNormalCurveTable
{
    internal static async ValueTask<NormalCurveTableReadModel> GetNormalCurveTableAsync(
        this GetNormalCurveTableQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetNormalCurveTableAsync();
}
