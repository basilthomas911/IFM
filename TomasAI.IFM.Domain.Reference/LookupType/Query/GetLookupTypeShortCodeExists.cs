using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query;

public static class GetLookupTypeShortCodeExists
{
    /// <summary>
    /// Handles the GetLookupTypeShortCodeExistsQuery by checking if a given short code exists for a specified lookup type in the reference database.
    /// It retrieves the list of short codes for the lookup type and checks for a match, returning a boolean result wrapped in a ScalarReadModel and ServiceResult.
    /// </summary>
    /// <param name="q">The query for checking short code existence.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask GetLookupTypeShortCodeExistsAsync(this GetLookupTypeShortCodeExistsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var shortCodes = await dbFactory.ReferenceDb.GetLookupTypeShortCodesAsync(q.LookupTypeName);
        var result =  new ScalarReadModel<bool>(shortCodes.Any(e => e.ShortCode.Equals(q.ShortCode, StringComparison.OrdinalIgnoreCase)));
        await context.ReplyAsync(q.Subject.ThreadId, GetLookupTypesQuery.Verb, new ServiceResult<ScalarReadModel<bool>>(result));
    }
}
