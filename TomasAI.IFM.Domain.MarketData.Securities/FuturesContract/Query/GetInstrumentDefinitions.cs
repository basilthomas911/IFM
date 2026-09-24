using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

/// <summary>Handles instrument-definition selection queries for the futures-contract actor.</summary>
public static class GetInstrumentDefinitions
{
    /// <summary>Reads the requested selection page and replies on the query thread.</summary>
    /// <param name="query">The instrument-definition selection request.</param>
    /// <param name="context">The owning query actor context.</param>
    /// <param name="cancellationToken">Cancels the storage read before a reply is sent.</param>
    /// <returns>The asynchronous query and reply operation.</returns>
    public static async ValueTask ExecuteAsync(
        this GetInstrumentDefinitionsQuery query,
        IFuturesContractQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.DbFactory.ReferenceDb.InstrumentDefinitions.GetSelectionPageAsync(
            query.Request, DateTimeOffset.UtcNow, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetInstrumentDefinitionsQuery.Verb,
            new ServiceResult<InstrumentDefinitionPage>(result));
    }
}
