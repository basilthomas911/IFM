using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public static class SpreadTradeDataViewModelExtension
{
    public static ITradePosition ToSpreadTradeData(this TradePositionReadModel e, DateTime createdOn, string createdBy)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new TradePosition(orderId: e.EntityId.OrderId, tradeId: e.EntityId.TradeId, tradeType: e.EntityId.TradeType, valueDate: e.EntityId.ValueDate, daysToExpiry: e.EntityId.DaysToExpiry, tradeStatus: e.EntityId.TradeStatus, createdOn: createdOn, createdBy: createdBy).SetCommission(e.Commission).SetAssetPrice(e.AssetPrice).SetOTMProbability(e.OTMProbability).SetForwardPrice(e.ForwardPrice).SetForwardLossRatio(e.ForwardLossRatio).SetLossProbability(e.LossProbability).SetRiskFreeRate(e.RiskFreeRate).AddOptionLegData(e.OptionLegData.Select(o => o.ToOptionLegData(e.EntityId, createdOn, createdBy, createdOn, createdBy)).ToArray());
    }
}
