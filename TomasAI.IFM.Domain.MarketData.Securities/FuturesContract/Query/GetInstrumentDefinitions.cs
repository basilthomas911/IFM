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
        /// <summary>Reads the requested selection page from the published definition catalog.</summary>
        ///
        /// <param name="context">The owning query actor context.</param>
        /// <param name="cancellationToken">Cancels the storage read.</param>
        /// <returns>The typed selection page result.</returns>
        public async ValueTask<ServiceResult<InstrumentDefinitionPage>> ExecuteAsync(
            IFuturesContractQueryContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (context.InstrumentDefinitions.GetSelectionPageAsync(
                query.Request, (context.Container?.Resolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow(), cancellationToken)).ConfigureAwait(false);
            return new ServiceResult<InstrumentDefinitionPage>(result);
        }
    }
}
