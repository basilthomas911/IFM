using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query;

public static class GetLookupTypeNames
{
    /// <summary>
    /// Handles the GetLookupTypeNamesQuery by retrieving lookup type names from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query for retrieving lookup type names.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask GetLookupTypeNamesAsync(this GetLookupTypeNamesQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var lookupTypeNames = await dbFactory.ReferenceDb.GetLookupTypeNamesAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetLookupTypeNamesQuery.Verb, new ServiceResult<string[]>([.. lookupTypeNames]));
    }
}
