using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

/// <summary>Handles instrument-definition selection queries for the futures-contract actor.</summary>
public static class GetInstrumentDefinitions
{

    /// <param name="query">
    /// The instrument-definition selection request.
    /// </param>
    extension(GetInstrumentDefinitionsQuery query)
    {
        /// <summary>Reads the requested selection page and replies on the query thread.</summary>
        ///
        /// <param name="context">The owning query actor context.</param>
        /// <param name="cancellationToken">Cancels the storage read before a reply is sent.</param>
        /// <returns>The asynchronous query and reply operation.</returns>
        public async ValueTask ExecuteAsync(
            IFuturesContractQueryContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (context.InstrumentDefinitions.GetSelectionPageAsync(
                query.Request, DateTimeOffset.UtcNow, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await (context.ReplyAsync(query.Subject.ThreadId, GetInstrumentDefinitionsQuery.Verb,
                new ServiceResult<InstrumentDefinitionPage>(result))).ConfigureAwait(false);
        }
    }
}
