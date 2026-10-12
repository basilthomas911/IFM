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
    public static async ValueTask<ScalarReadModel<int>> ExecuteAsync(
        this GetNextSeedIdQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(q);
        return new(await (dbFactory.ReferenceDb.GetNextSeedIdAsync(q.SeedType, cancellationToken)).ConfigureAwait(false));
    }

    /// <summary>Reads and replies with the requested Reference result.</summary>
    public static async ValueTask ExecuteAsync(this GetNextSeedIdQuery query, Actor.IReferenceQueryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<ScalarReadModel<int>>(result)).ConfigureAwait(false);
    }
}
