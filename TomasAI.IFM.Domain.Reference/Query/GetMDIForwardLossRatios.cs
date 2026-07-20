using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetMDIForwardLossRatios
{
    /// <summary>
    /// Handles a request to retrieve MDI forward loss ratios for the trend direction and trade type carried by the query.
    /// </summary>
    public static async ValueTask GetMDIForwardLossRatiosAsync(this GetMDIForwardLossRatiosQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.ReferenceDb.GetMDIForwardLossRatiosAsync(q.TrendDirection, q.TradeType);
        await context.ReplyAsync(q.Subject.ThreadId, GetMDIForwardLossRatiosQuery.Verb, new ServiceResult<MDIForwardLossRatioReadModel[]>([.. result]));
    }
}
