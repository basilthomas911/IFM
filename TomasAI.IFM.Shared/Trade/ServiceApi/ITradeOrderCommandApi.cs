using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Shared.Trade.ServiceApi
{
    public interface ITradeOrderCommandApi
    {
        Task<ServiceResult<Guid>> SubmitTradeOrderAsync(TradeOrderReadModel tradeOrder);
        Task<ServiceResult<Guid>> SubmitCancelOrderAsync(TradeOrderEntityId tradeOrderId);
        Task<ServiceResult<Guid>> SubmitUpdateOrderAsync(TradeOrderEntityId tradeOrderId, decimal orderPrice);
        Task<ServiceResult<Guid>> OpenTradeOrderAsync(TradeOrderEntityId tradeOrderId, bool executed = false, string errorMessage = "");
        Task<ServiceResult<Guid>> FillTradeOrderAsync(TradeOrderEntityId tradeOrderId, TradeFillReadModel tradeFill, bool executed = false, string errorMessage = "");
        Task<ServiceResult<Guid>> UpdateOrderAsync(TradeOrderEntityId tradeOrderId, decimal orderPrice, bool executed = false, string errorMessage = "");
        Task<ServiceResult<Guid>> CloseTradeOrderAsync(TradeOrderEntityId tradeOrderId, bool executed = false, string errorMessage = "");
        Task<ServiceResult<Guid>> CancelTradeOrderAsync(TradeOrderEntityId tradeOrderId, bool executed = false, string errorMessage = "");
    }
}
