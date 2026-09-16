using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetCurrentSeedId
{
    /// <summary>
    /// Retrieves the highest ID range currently reserved in PostgreSQL for the specified seed type.
    /// </summary>
    /// <param name="q">The query for retrieving the current seed ID.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask<ScalarReadModel<int>> ExecuteAsync(
        this GetCurrentSeedIdQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        => new(await (cancellationToken.CanBeCanceled
            ? dbFactory.ReferenceDb.GetCurrentSeedIdAsync(q.SeedType, cancellationToken)
            : dbFactory.ReferenceDb.GetCurrentSeedIdAsync(q.SeedType)));

    /// <summary>Reads and replies with the requested Reference result.</summary>
    public static async ValueTask ExecuteAsync(this GetCurrentSeedIdQuery query, Actor.IReferenceQueryContext context, CancellationToken cancellationToken)
    {
        var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<ScalarReadModel<int>>(result)).ConfigureAwait(false);
    }
}
