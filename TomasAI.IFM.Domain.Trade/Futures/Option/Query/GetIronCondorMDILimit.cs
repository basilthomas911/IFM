using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetIronCondorMDILimit
{
    /// <summary>Reads the current legacy Iron Condor market-data limit from the blackboard.</summary>
    public static async ValueTask ExecuteAsync(
        this GetIronCondorMDILimitQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = context.BlackboardService.Trade.IronCondorMDILimit.Get(
            new OptionTradeEntityId(query.OrderId, query.TradeId), query.ValueDate);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<IronCondorMDILimitDataModel?>(result)).ConfigureAwait(false);
    }
}
