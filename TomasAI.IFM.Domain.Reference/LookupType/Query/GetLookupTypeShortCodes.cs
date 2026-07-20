using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query;

public static class GetLookupTypeShortCodes
{
    /// <summary>
    /// Handles the GetLookupTypeShortCodesQuery by retrieving the lookup type short codes from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query for retrieving lookup type short codes.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask GetLookupTypeShortCodesAsync(this GetLookupTypeShortCodesQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var lookupTypeShortCodes = await dbFactory.ReferenceDb.GetLookupTypeShortCodesAsync(q.LookupTypeName);
        await context.ReplyAsync(q.Subject.ThreadId, GetLookupTypeShortCodesQuery.Verb, new ServiceResult<LookupTypeShortCodeReadModel[]>([.. lookupTypeShortCodes]));
    }
}
