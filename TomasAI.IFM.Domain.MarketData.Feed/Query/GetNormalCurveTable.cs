using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetNormalCurveTable
{
    internal static async ValueTask<NormalCurveTableReadModel> ExecuteAsync(
        this GetNormalCurveTableQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetNormalCurveTableAsync();

    /// <summary>Reads and replies with the requested market-data feed result.</summary>
    public static async ValueTask ExecuteAsync(
        this GetNormalCurveTableQuery query,
        TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
        MarketDataFeedQueryParameters parameters)
    {
        var result = await query.ExecuteAsync(parameters.DbFactory).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new TomasAI.IFM.Shared.EventSourcing.ServiceResult<NormalCurveTableReadModel>(result)).ConfigureAwait(false);
    }
}
