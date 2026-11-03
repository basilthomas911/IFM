using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetCurrentSeedId
{

    /// <param name="q">
    /// The query for retrieving the current seed ID.
    /// </param>
    extension(GetCurrentSeedIdQuery q)
    {
        /// <summary>
        /// Retrieves the highest ID range currently reserved in PostgreSQL for the specified seed type.
        /// </summary>
        ///
        /// <param name="context">The query actor context.</param>
        /// <param name="dbFactory">The database context factory.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async ValueTask<ScalarReadModel<int>> ExecuteAsync(
        IDbContextFactory dbFactory, CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            return new(await (dbFactory.ReferenceDb.GetCurrentSeedIdAsync(q.SeedType, cancellationToken)).ConfigureAwait(false));
        }
    }

    extension(GetCurrentSeedIdQuery query)
    {
        /// <summary>Reads and replies with the requested Reference result.</summary>
        public async ValueTask ExecuteAsync(Actor.IReferenceQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<ScalarReadModel<int>>(result)).ConfigureAwait(false);
        }
    }
}
