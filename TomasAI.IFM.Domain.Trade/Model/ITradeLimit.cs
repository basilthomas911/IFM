using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeLimit
{
    int TradeId { get; }
    TradeType TradeType { get; }
    decimal RiskMargin { get; }
    decimal MaxProfit { get; }
    decimal MaxLoss { get; }
    decimal MaxReturn { get; }
    decimal MaxLossLimit { get; }
    decimal MinProfitLimit { get; }
    decimal MinProfitTarget { get; }
    decimal DailyProfitTarget { get; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    DateTime UpdatedOn { get; }
    string UpdatedBy { get; }

    TradeLimitReadModel ToViewModel();
    ITradeLimit SetDailyProfitTarget(decimal dailyProfitTarget);
}
