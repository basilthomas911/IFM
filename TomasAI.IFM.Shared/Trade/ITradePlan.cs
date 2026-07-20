using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade
{
    public interface ITradePlan
    {
        int OrderId { get; }
        DateTime TradeDate { get; }
        DateOnly valueDate { get; }
        DateTime MaturityDate { get; }
        DateTime ActionDate { get; }
        ActionType ActionType { get; }
        ActionSubType ActionSubType { get; }
        ActionState ActionState { get; }
        string ActionReason { get; }
        decimal TradePnl { get; }
        double ForwardLossRatio { get; }
        double LossProbability { get; }
        double MScore { get; }
        decimal MaxProfit { get; }
        decimal MaxLoss { get; }
        decimal DailyProfitTarget { get; }
        decimal AssetPrice { get; }
        double AssetStdDev { get; }
        double AssetMean { get; }
        double AssetPriceChange { get; }
        MarketDirectionType MarketTrend { get; }
        MarketVolatilityType MarketVolatility { get; }
        double PutSpreadStdDev { get; }
        double PutSpreadProbability { get; }
        double PutSpreadActualProbability { get; }
        double CallSpreadStdDev { get; }
        double CallSpreadProbability { get; }
        double CallSpreadActualProbability { get; }
        decimal NetPrice { get; }
        decimal ForwardPrice { get; }
        double StopLossLimit { get; }
        DateTime CreatedOn { get; }
        string CreatedBy { get; }

        TradePlanReadModel ToViewModel();
    }
}
