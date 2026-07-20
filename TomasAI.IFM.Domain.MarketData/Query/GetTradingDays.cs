using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetTradingDays
{
    /// <summary>
    /// Handles the GetTradingDaysQuery by retrieving the number of trading days between the specified start and end dates for a given market and currency type. 
    /// The result is sent back to the query actor context.
    /// </summary>
    /// <param name="q">The query for which to retrieve the number of trading days.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
	public static async ValueTask GetTradingDaysAsync(this GetTradingDaysQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var tradingDates = await dbFactory.MarketDataDb.GetTradingDatesAsync(q.StartDate, q.EndDate, q.MarketType, q.CurrencyType);
        await context.ReplyAsync(q.Subject.ThreadId, GetTradingDaysQuery.Verb, new ServiceResult<ScalarReadModel<int>>(new ScalarReadModel<int>(tradingDates.Length)));
    }
}
