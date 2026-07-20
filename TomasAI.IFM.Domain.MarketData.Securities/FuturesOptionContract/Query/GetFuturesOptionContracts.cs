using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContracts
{
    /// <summary>
    /// Handles a request to retrieve all futures option contracts by symbol.
    /// </summary>
    public static async ValueTask GetFuturesOptionContractsAsync(
        this GetFuturesOptionContractsQuery q,IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var futuresOptionContracts = await dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(q.Symbol);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractsQuery.Verb, new ServiceResult<FuturesOptionContractReadModel[]>([.. futuresOptionContracts]));
    }
}
