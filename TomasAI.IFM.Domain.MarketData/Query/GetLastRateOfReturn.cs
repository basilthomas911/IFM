using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetLastRateOfReturn
{
    /// <summary>
    /// Handles the GetLastRateOfReturnQuery by retrieving the last rate of return for a given symbol from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query for which to retrieve the last rate of return.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
	internal static async ValueTask GetLastRateOfReturnAsync(this GetLastRateOfReturnQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var result = await dbFactory.MarketDataDb.GetLastRateOfReturnAsync(q.Symbol);
        await context.ReplyAsync(q.Subject.ThreadId, GetLastRateOfReturnQuery.Verb, new ServiceResult<RateOfReturnReadModel>(result));
    }
}
    
