using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.Shared;

public class SpreadDistribution
{
    readonly int _daysToMaturity;
    readonly double _forwardPrice;

    public SpreadDistribution(int daysToMaturity, double forwardPrice)
    {
        _daysToMaturity = daysToMaturity;
        _forwardPrice = forwardPrice;
    }

    public SpreadDistributionReadModel ToViewModel(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, LossProbabilityDataModel lossProbability)
        
    {
        ArgumentNullException.ThrowIfNull(lossProbability);
        return new SpreadDistributionReadModel(
            id: 0,
            tradeId: tradeId,
            tradeType: tradeType,
            tradeStatus: tradeStatus,
            valueDate: valueDate,
            daysToExpiry: _daysToMaturity,
            forwardPrice: _forwardPrice,
            lossProbability: lossProbability.Value,
            lossThreshold: lossProbability.Threshold,
            lossThresholdCount: lossProbability.ThresholdCount,
            shortVolatility: 0,
            longVolatility: 0,
            forwardLossRatio: 0,
            createdOn: DateTime.Now
        );
    }

}
