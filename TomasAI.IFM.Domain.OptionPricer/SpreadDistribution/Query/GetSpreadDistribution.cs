using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query;

public static class GetSpreadDistribution
{
    internal static async ValueTask GetSpreadDistributionAsync(this GetSpreadDistributionQuery q, IQueryActorContext context, IDbContextFactory dbFactory,
        int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        var result = await dbFactory.OptionPricerDb.GetSpreadDistributionAsync(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);
        await context.ReplyAsync(q.Subject.ThreadId, GetSpreadDistributionQuery.Verb, new ServiceResult<SpreadDistributionReadModel?>(result));
    }
}
