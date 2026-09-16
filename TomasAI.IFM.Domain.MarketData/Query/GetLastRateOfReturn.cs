using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetLastRateOfReturn
{
    /// <summary>
    /// Handles the GetLastRateOfReturnQuery by retrieving the last rate of return for a given symbol from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query for which to retrieve the last rate of return.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
	internal static ValueTask<RateOfReturnReadModel> ExecuteAsync(
        this GetLastRateOfReturnQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => new(cancellationToken.CanBeCanceled
            ? dbFactory.MarketDataDb.GetLastRateOfReturnAsync(q.Symbol, cancellationToken)
            : dbFactory.MarketDataDb.GetLastRateOfReturnAsync(q.Symbol));

    /// <summary>Reads and replies with the requested market-data result.</summary>
    public static async ValueTask ExecuteAsync(
        this GetLastRateOfReturnQuery query,
        TomasAI.IFM.Domain.MarketData.Query.Actor.IMarketDataQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<RateOfReturnReadModel>(result)).ConfigureAwait(false);
    }
}
    
