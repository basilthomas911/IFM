using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

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
    public static async ValueTask<ScalarReadModel<int>> GetNextSeedIdAsync(
        this GetNextSeedIdQuery q, IDbContextFactory dbFactory)
        => new(await dbFactory.ReferenceDb.GetNextSeedIdAsync(q.SeedType));
}
