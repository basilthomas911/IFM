using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;

namespace TomasAI.IFM.Domain.Trade.Option.Query;

internal static class GetOptionLegContractIds
{
    /// <summary>
    /// Gets the contract ids for the option legs.
    /// </summary>
    /// <param name="q">The query containing the trade identifier for which to retrieve option leg contract identifiers.</param>
    /// <param name="context">The query actor context used to send the reply containing the result.</param>
    /// <param name="dbFactory">The database context factory used to access option leg contract identifiers.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async ValueTask GetOptionLegContractIdsAsync(this GetOptionLegContractIdsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.TradeDb.GetOptionLegContractIdsAsync(q.TradeId);
        await context.ReplyAsync(q.Subject.ThreadId, GetOptionLegContractIdsQuery.Verb, new ServiceResult<string[]>([.. result]));
    }

}
