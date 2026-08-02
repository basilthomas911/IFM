using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetMDIForwardLossRatios
{
    /// <summary>
    /// Handles a request to retrieve MDI forward loss ratios for the trend direction and trade type carried by the query.
    /// </summary>
    public static async ValueTask<MDIForwardLossRatioReadModel[]> GetMDIForwardLossRatiosAsync(
        this GetMDIForwardLossRatiosQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.ReferenceDb.GetMDIForwardLossRatiosAsync(q.TrendDirection, q.TradeType)];
}
