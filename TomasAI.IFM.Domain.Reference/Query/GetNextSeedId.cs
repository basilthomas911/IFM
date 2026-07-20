using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetNextSeedId
{
    /// <summary>
    /// Gets the next seed ID for a given seed type from the database and replies to the query actor context with the result.
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask GetNextSeedIdAsync(this GetNextSeedIdQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.ReferenceDb.GetNextSeedIdAsync(q.SeedType);
        await context.ReplyAsync(q.Subject.ThreadId, GetNextSeedIdQuery.Verb, new ServiceResult<ScalarReadModel<int>>(new ScalarReadModel<int>(result)));
    }
}
