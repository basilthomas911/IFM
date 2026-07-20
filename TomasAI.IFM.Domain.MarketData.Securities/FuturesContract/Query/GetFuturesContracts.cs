using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetFuturesContracts
{
    public static async ValueTask GetFuturesContractsAsync(this GetFuturesContractsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var futuresContracts = await dbFactory.SecuritiesDb.GetFuturesContractsAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesContractsQuery.Verb, new ServiceResult<FuturesContractV2ReadModel[]>([.. futuresContracts]));
    }
}
