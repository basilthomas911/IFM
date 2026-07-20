using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer;

public class SpreadDistribution
{
    int _daysToMaturity;
    double _forwardPrice;

    public SpreadDistribution(int daysToMaturity,  double forwardPrice)
    { 
        _daysToMaturity = daysToMaturity;
        _forwardPrice = forwardPrice;
    }

    public SpreadDistributionReadModel ToViewModel(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, LossProbabilityDataModel lossProbability )
        => new SpreadDistributionReadModel (
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
