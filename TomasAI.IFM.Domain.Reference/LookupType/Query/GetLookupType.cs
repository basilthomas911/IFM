using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Queries;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query;

public static class GetLookupType
{
    /// <summary>
    /// Handles the GetLookupTypeQuery by retrieving the specified lookup type from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query for retrieving the lookup type.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask GetLookupTypeAsync(this GetLookupTypeQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var lookupType =  await dbFactory.ReferenceDb.GetLookupTypeAsync(q.LookupTypeName);
        await context.ReplyAsync(q.Subject.ThreadId, GetLookupTypesQuery.Verb, new ServiceResult<LookupTypeCollection>([.. lookupType]));
    }
}
