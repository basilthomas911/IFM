using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query;

public static class GetSpreadDistribution
{
    internal static async ValueTask<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        this GetSpreadDistributionQuery q, IDbContextFactory dbFactory,
        int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        return await dbFactory.OptionPricerDb.GetSpreadDistributionAsync(
            tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);
    }
}
