using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query;

public static class GetLookupTypes
{
    /// <summary>
    /// Gets the lookup types from the database and replies to the query actor context with the result.
    /// </summary>
    /// <param name="q">The query for retrieving lookup types.</param>
    /// <param name="context">The query actor context for sending the reply.</param>
    /// <param name="dbFactory">The database context factory for accessing reference storage.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask<LookupTypeCollection> GetLookupTypesAsync(
        this GetLookupTypesQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        => [.. await (cancellationToken.CanBeCanceled
            ? dbFactory.ReferenceDb.GetLookupTypesAsync(cancellationToken)
            : dbFactory.ReferenceDb.GetLookupTypesAsync())];
}
