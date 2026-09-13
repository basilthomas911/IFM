using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

public static class SpreadDistributionReadModelMappingExtensions
{
    public static SpreadDistributionReadModel ToSpreadDistributionReadModel(
        this TradeSpreadDistributionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SpreadDistributionReadModel(
            snapshot.Id,
            snapshot.TradeId,
            snapshot.ValueDate,
            snapshot.TradeType,
            snapshot.TradeStatus,
            snapshot.DaysToExpiry,
            snapshot.ForwardPrice,
            snapshot.LossProbability,
            snapshot.LossThreshold,
            snapshot.LossThresholdCount,
            snapshot.ShortVolatility,
            snapshot.LongVolatility,
            snapshot.ForwardLossRatio,
            snapshot.CreatedOn);
    }
}
