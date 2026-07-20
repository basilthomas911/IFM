using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetFuturesContract
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask GetFuturesContractAsync(
        this GetFuturesContractQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var futuresContract = await dbFactory.SecuritiesDb.GetFuturesContractAsync(q.ContractId);
        await context.ReplyAsync(q.Subject.ThreadId, GetCurrentlyTradedFuturesContractsQuery.Verb, new ServiceResult<FuturesContractV2ReadModel?>(futuresContract));
    }
}
