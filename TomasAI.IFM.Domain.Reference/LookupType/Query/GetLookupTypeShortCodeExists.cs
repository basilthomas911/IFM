using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

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
    public static async ValueTask<ScalarReadModel<bool>> GetLookupTypeShortCodeExistsAsync(
        this GetLookupTypeShortCodeExistsQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
    {
        return new ScalarReadModel<bool>(await (cancellationToken.CanBeCanceled
            ? dbFactory.ReferenceDb.LookupTypeShortCodeExistsAsync(q.LookupTypeName, q.ShortCode, cancellationToken)
            : dbFactory.ReferenceDb.LookupTypeShortCodeExistsAsync(q.LookupTypeName, q.ShortCode)));
    }
}
