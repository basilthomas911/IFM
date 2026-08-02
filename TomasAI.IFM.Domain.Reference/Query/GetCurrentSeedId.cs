using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetCurrentSeedId
{
    /// <summary>
    /// Handles the GetCurrentSeedIdQuery by retrieving the current seed ID for the specified seed type from the reference database and replying with the result.
    /// </summary>
    /// <param name="q">The query for retrieving the current seed ID.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask<ScalarReadModel<int>> GetCurrentSeedIdAsync(
        this GetCurrentSeedIdQuery q, IDbContextFactory dbFactory)
        => new(await dbFactory.ReferenceDb.GetCurrentSeedIdAsync(q.SeedType));
}
