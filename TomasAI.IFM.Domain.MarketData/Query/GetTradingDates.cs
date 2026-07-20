using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetTradingDates
{
    /// <summary>
    /// Handles the GetTradingDatesQuery by retrieving trading dates from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query for which to retrieve trading dates.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>

	public static async ValueTask GetTradingDatesAsync(this GetTradingDatesQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var tradingDates = await dbFactory.MarketDataDb.GetTradingDatesAsync(q.StartDate, q.EndDate, q.MarketType, q.CurrencyType);
        await context.ReplyAsync(q.Subject.ThreadId, GetTradingDatesQuery.Verb, new ServiceResult<DateOnly[]>(tradingDates));
    }
}
