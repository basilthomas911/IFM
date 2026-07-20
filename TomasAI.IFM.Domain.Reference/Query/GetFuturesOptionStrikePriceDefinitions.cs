using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetFuturesOptionStrikePriceDefinitions
{
    /// <summary>
    /// Handles the GetFuturesOptionStrikePriceDefinitionsQuery by retrieving the minimum, maximum, and increment values for futures option strike prices from the reference database and replying with the results.
    /// </summary>
    /// <param name="q">The query to handle.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask GetFuturesOptionStrikePriceDefinitionsAsync(this GetFuturesOptionStrikePriceDefinitionsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await GetFuturesOptionStrikePriceDefinitionsAsync(dbFactory.ReferenceDb);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionStrikePriceDefinitionsQuery.Verb, new ServiceResult<FuturesOptionStrikePriceReadModel>(result));

        static async ValueTask<FuturesOptionStrikePriceReadModel> GetFuturesOptionStrikePriceDefinitionsAsync( IReferenceDbContext db)
           => new()
           {
               Minimum = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceMin")).FirstOrDefault()?.ShortCode),
               Maximum = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceMax")).FirstOrDefault()?.ShortCode),
               Increment = Convert.ToInt32((await db.GetLookupTypeAsync("FuturesOptionStrikePriceIncrement")).FirstOrDefault()?.ShortCode)
           };
    }
}
