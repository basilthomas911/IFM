using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetCurrentlyTradedFuturesContracts
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask GetCurrentlyTradedFuturesContractsAsync(
        this GetCurrentlyTradedFuturesContractsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var futuresContracts = await dbFactory.SecuritiesDb.GetCurrentlyTradedFuturesContractsAsync(q.Symbol);
        await context.ReplyAsync(q.Subject.ThreadId, GetCurrentlyTradedFuturesContractsQuery.Verb, new ServiceResult<FuturesContractV2ReadModel[]>([.. futuresContracts]));
    }
}
