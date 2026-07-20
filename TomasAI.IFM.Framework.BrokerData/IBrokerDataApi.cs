using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;


namespace TomasAI.IFM.Framework.BrokerData
{
    public interface IBrokerDataApi
    {
        Task OpenBrokerOrderAsync(TradeOrderReadModel tradeOrder);
        Task CloseBrokerOrderAsync(TradeOrderEntityId tradeOrderId);
        Task UpdateBrokerOrderAsync(TradeOrderEntityId tradeOrderId, decimal orderPrice);
        Task CancelBrokerOrderAsync(TradeOrderEntityId tradeOrderId);
    }
}