using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradePosition
{
    TradePositionEntityId Id { get; }
    int OrderId { get; }
    int TradeId { get; }
    TradeType TradeType { get; }
    DateOnly ValueDate { get; }
    int DaysToExpiry { get; }
    TradeStatus TradeStatus { get; }
    decimal Commission { get; }
    int DeltaHedge { get; }
    decimal NetSpread { get; }
    decimal TradeValue { get; }
    decimal TradePnl { get; }
    decimal AssetPrice { get; }
    double OTMProbability { get; }
    decimal ForwardPrice { get; }
    double ForwardLossRatio { get; }
    double LossProbability { get; }
    double RiskFreeRate { get; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    DateTime UpdatedOn { get; }
    string UpdatedBy { get; }
    IOptionLegDataCollection OptionLegData { get; }

    TradePositionReadModel ToViewModel();
    ITradePosition AddOptionLegData(ICollection<IOptionLegData> optionLegData);
    ITradePosition ReplaceOptionLegData(IOptionLegData optionLegData);
    ITradePosition SetTradePnl(decimal tradePnl);
    ITradePosition SetTradeStatus(TradeStatus tradeStatus);
    ITradePosition SetCommission(decimal commission);
    ITradePosition SetAssetPrice(decimal assetPrice);
    ITradePosition SetOTMProbability(double otmProbability);
    ITradePosition SetForwardPrice(decimal maxPrice);
    ITradePosition SetForwardLossRatio(double forwardLossRatio);
    ITradePosition SetLossProbability(double lossProbability);
    ITradePosition SetRiskFreeRate(double riskFreeRate);
    ITradePosition SetUpdated(DateTime updatedOn, string updatedBy);
    ITradePosition SetEndOfDayStatus();
}
